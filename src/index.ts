import './styles/main.less';
import { OS } from './core/os';

// Initialize the operating system when the page loads
document.addEventListener('DOMContentLoaded', () => {
  const params = new URLSearchParams(window.location.search);
  const monitorIdParam = params.get('monitor');
  const isSecondary = monitorIdParam !== null || !!window.opener;

  const os = new OS();
  if (isSecondary) {
    os.getMultiMonitorManager().connectAsSecondary();

    document.getElementById('taskbar')?.remove();
    document.getElementById('desktop-icons')?.remove();

    const id = monitorIdParam ? parseInt(monitorIdParam, 10) : 2;
    document.querySelectorAll('.windows-container').forEach(el => {
      if (el.id !== `windows-container-${id}`) {
        (el as HTMLElement).style.display = 'none';
      }
    });
  }

  os.init();
});
