import './styles.css';
import { OS } from './core/os';

// Initialize the operating system when the page loads
document.addEventListener('DOMContentLoaded', () => {
  const os = new OS();
  os.init();
});
