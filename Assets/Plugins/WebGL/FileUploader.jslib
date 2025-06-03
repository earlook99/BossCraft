mergeInto(LibraryManager.library, {
    OpenImagePicker: function(objectName, methodName) {
        var gameObjectName = UTF8ToString(objectName);
        var methodNameStr = UTF8ToString(methodName);
        
        if (document.getElementById('unityFileInput')) {
            document.getElementById('unityFileInput').remove();
        }
        
        var fileInput = document.createElement('input');
        fileInput.id = 'unityFileInput';
        fileInput.type = 'file';
        fileInput.accept = 'image/*';
        fileInput.style.display = 'none';
        
        fileInput.onchange = function(event) {
            var file = event.target.files[0];
            if (!file) return;
            
            var reader = new FileReader();
            reader.onload = function(e) {
                var base64 = e.target.result;
                SendMessage(gameObjectName, methodNameStr, base64);
            };
            reader.readAsDataURL(file);
        };
        
        document.body.appendChild(fileInput);
        fileInput.click();
    },
    
    DownloadImage: function(filenamePtr, base64DataPtr) {
        var filename = UTF8ToString(filenamePtr);
        var base64Data = UTF8ToString(base64DataPtr);
        
        var link = document.createElement('a');
        link.download = filename;
        link.href = base64Data;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
});