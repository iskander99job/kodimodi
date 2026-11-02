mergeInto(LibraryManager.library, {
  CopyTextToClipboard: function (textPtr) {
    // Преобразуем указатель из памяти Unity в строку JavaScript
    var text = UTF8ToString(textPtr);
    
    // Используем современный API (работает в защищенном контексте https)
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function() {
        console.log('Текст успешно скопирован в буфер обмена.');
      }).catch(function(err) {
        console.error('Ошибка копирования: ', err);
      });
    } else {
      // Резервный вариант для старых браузеров
      var textArea = document.createElement("textarea");
      textArea.value = text;
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand("copy");
      document.body.removeChild(textArea);
      console.log('Текст скопирован (fallback).');
    }
  }
});