const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');

app.disableHardwareAcceleration();

function createWindow() {
  const win = new BrowserWindow({
    width: 1180,
    height: 820,
    minWidth: 920,
    minHeight: 680,
    backgroundColor: '#f5f7fb',
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), contextIsolation: true, nodeIntegration: false }
  });
  win.setMenuBarVisibility(false);
  win.loadFile(path.join(__dirname, '../dist/index.html'));
}

app.whenReady().then(() => {
  ipcMain.handle('save-file', async (_event, { data, defaultPath, filters }) => {
    const result = await dialog.showSaveDialog({ defaultPath, filters });
    if (result.canceled || !result.filePath) return { canceled: true };
    const base64 = data.replace(/^data:[^;]+;base64,/, '');
    fs.writeFileSync(result.filePath, Buffer.from(base64, 'base64'));
    return { canceled: false, filePath: result.filePath };
  });
  ipcMain.handle('print-window', async (event) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    return new Promise((resolve) => win.webContents.print({ silent: false, printBackground: true }, (success, reason) => resolve({ success, reason })));
  });
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
