mergeInto(LibraryManager.library, {
    DetectMediaBrowser: function (receiverPtr) {
        var receiver = UTF8ToString(receiverPtr);
        var ua = navigator.userAgent || "";
        var browser = "unknown";

        if (/Edg\/|EdgiOS\/|EdgA\//i.test(ua)) {
            browser = "edge";
        } else if (/OPR\/|Opera|OPiOS\//i.test(ua)) {
            browser = "unknown";
        } else if (/Firefox\/|FxiOS\//i.test(ua)) {
            browser = "firefox";
        } else if (/Chrome\/|CriOS\//i.test(ua)) {
            browser = "chrome";
        } else if (/Safari\//i.test(ua)) {
            browser = "safari";
        }

        SendMessage(receiver, "OnBrowserDetected", browser);
    },

    CheckCameraPermission: function (receiverPtr) {
        var receiver = UTF8ToString(receiverPtr);
        var existing = window.__profileMediaPermissions;

        // Повторный вызов от Play использует ту же подписку.
        if (existing && existing.receiver === receiver && existing.active) {
            existing.refresh();
            return;
        }

        if (existing) {
            existing.cleanup();
        }

        var context = {
            receiver: receiver,
            active: true,
            statuses: [],
            requestPending: false,
            refreshing: false,
            refreshAgain: false
        };

        window.__profileMediaPermissions = context;

        function check(name) {
            if (!navigator.permissions || !navigator.permissions.query) {
                return Promise.resolve(null);
            }

            return Promise.resolve()
                .then(function () {
                    return navigator.permissions.query({ name: name });
                })
                .catch(function () {
                    return null;
                });
        }

        function report() {
            if (!context.active) return;

            var camera = context.statuses[0];
            var microphone = context.statuses[1];

            SendMessage(
                context.receiver,
                "OnMediaPermissionsChecked",
                (camera ? camera.state : "unknown") + "|" +
                (microphone ? microphone.state : "unknown")
            );
        }

        function unbind() {
            context.statuses.forEach(function (status) {
                if (status) {
                    status.removeEventListener("change", report);
                }
            });
        }

        context.refresh = function () {
            if (!context.active) return;

            if (context.refreshing) {
                context.refreshAgain = true;
                return;
            }

            context.refreshing = true;

            Promise.all([check("camera"), check("microphone")])
                .then(function (statuses) {
                    if (!context.active) return;

                    unbind();
                    context.statuses = statuses;

                    context.statuses.forEach(function (status) {
                        if (status) {
                            status.addEventListener("change", report);
                        }
                    });

                    context.refreshing = false;
                    report();

                    if (context.refreshAgain) {
                        context.refreshAgain = false;
                        context.refresh();
                    }
                });
        };

        function onFocus() {
            context.refresh();
        }

        function onVisibilityChange() {
            if (document.visibilityState === "visible") {
                context.refresh();
            }
        }

        context.cleanup = function () {
            context.active = false;
            unbind();

            window.removeEventListener("focus", onFocus);
            document.removeEventListener(
                "visibilitychange",
                onVisibilityChange
            );
        };

        window.addEventListener("focus", onFocus);
        document.addEventListener(
            "visibilitychange",
            onVisibilityChange
        );

        context.refresh();
    },

    RequestCameraAndMicrophone: function (receiverPtr, requestVideo) {
        var receiver = UTF8ToString(receiverPtr);
        var context = window.__profileMediaPermissions;

        if (!context || !context.active || context.receiver !== receiver) {
            SendMessage(
                receiver,
                "OnMediaPermissionRequested",
                "unavailable"
            );
            return;
        }

        if (context.requestPending) return;

        context.requestPending = true;

        function reply(result) {
            context.requestPending = false;

            // После выхода из сцены результат старого запроса игнорируем.
            if (!context.active) return;

            SendMessage(
                receiver,
                "OnMediaPermissionRequested",
                result
            );

            context.refresh();
        }

        if (!navigator.mediaDevices ||
            !navigator.mediaDevices.getUserMedia) {
            reply("unavailable");
            return;
        }

        try {
            navigator.mediaDevices.getUserMedia({
                video: requestVideo !== 0,
                audio: true
            })
            .then(function (stream) {
                // Здесь получаем доступ, но не передаём голос.
                // Захват для матча позднее запустит LiveKit.
                stream.getTracks().forEach(function (track) {
                    track.stop();
                });

                reply("granted");
            })
            .catch(function (error) {
                reply(error.name || "error");
            });
        } catch (error) {
            reply(error.name || "error");
        }
    },

    StopMediaPermissionWatch: function (receiverPtr) {
        var receiver = UTF8ToString(receiverPtr);
        var context = window.__profileMediaPermissions;

        if (context && context.receiver === receiver) {
            context.cleanup();
            window.__profileMediaPermissions = null;
        }
    }
});