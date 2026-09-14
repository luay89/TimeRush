const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('barcodeAPI', {
  saveFile: (payload) => ipcRenderer.invoke('save-file', payload),
  print: (copies) => ipcRenderer.invoke('print-window', copies)
});
