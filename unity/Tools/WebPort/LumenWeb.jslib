mergeInto(LibraryManager.library, {
  LumenWebReady: function (detailPointer) {
    var detail = JSON.parse(UTF8ToString(detailPointer));
    window.lumenUnityReady = detail;
    window.dispatchEvent(new CustomEvent('lumen-unity-ready', { detail: detail }));
  }
});
