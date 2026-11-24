mergeInto(LibraryManager.library, {
    Kodimodi_SaveAccessToken: function (tokenPointer) {
        try {
            var token = UTF8ToString(tokenPointer);
            var parts = token.split(".");
            if (parts.length !== 3) return 0;

            var encoded = parts[1].replace(/-/g, "+").replace(/_/g, "/");
            while (encoded.length % 4 !== 0) encoded += "=";

            var expires = JSON.parse(atob(encoded)).exp;
            var remaining = expires - Math.floor(Date.now() / 1000);
            if (!Number.isSafeInteger(remaining) || remaining <= 0) return 0;

            var https = window.location.protocol === "https:";
            var host = window.location.hostname;
            if (!https && host !== "localhost" && host !== "127.0.0.1") return 0;

            var cookie = "kodimodi_access_token=" + encodeURIComponent(token);
            document.cookie = cookie + "; Path=/; SameSite=Lax; Max-Age=" +
                remaining + (https ? "; Secure" : "");

            return document.cookie.split(";").some(function (item) {
                return item.trim() === cookie;
            }) ? 1 : 0;
        } catch (_) {
            return 0;
        }
    },

    Kodimodi_ReadAccessToken: function () {
        try {
            var name = "kodimodi_access_token=";
            var cookie = document.cookie.split(";").map(function (item) {
                return item.trim();
            }).find(function (item) {
                return item.indexOf(name) === 0;
            });

            if (!cookie) return 0;

            var token = decodeURIComponent(cookie.substring(name.length));
            if (!token) return 0;

            var size = lengthBytesUTF8(token) + 1;
            var buffer = _malloc(size);
            if (!buffer) return 0;

            stringToUTF8(token, buffer, size);
            return buffer;
        } catch (_) {
            return 0;
        }
    },

    Kodimodi_ClearAccessToken: function () {
        try {
            var name = "kodimodi_access_token=";
            var secure = window.location.protocol === "https:" ? "; Secure" : "";

            document.cookie = name + "; Path=/; SameSite=Lax; Max-Age=0" + secure;

            var stillPresent = document.cookie.split(";").some(function (item) {
                return item.trim().indexOf(name) === 0;
            });

            return stillPresent ? 0 : 1;
        } catch (_) {
            return 0;
        }
    }
});