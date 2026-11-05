mergeInto(LibraryManager.library, {
  ClearRoomFromUrl: function () {
    try {
      var url = new URL(window.location.href);
      if (!url.searchParams.has("room")) return;
      url.searchParams.delete("room");
      window.history.replaceState({}, document.title, url.pathname + url.search + url.hash);
    } catch (e) {
      console.warn("ClearRoomFromUrl failed", e);
    }
  }
});