import { GuiApplication } from '../core/gui-application';
import { OS } from '../core/os';

/**
 * HackPaint - minimal image editor application
 * Provides basic drawing and image loading/export features.
 */
export class HackPaintApp extends GuiApplication {
  private canvas: HTMLCanvasElement | null = null;
  private ctx: CanvasRenderingContext2D | null = null;
  private colorInput: HTMLInputElement | null = null;
  private sizeInput: HTMLInputElement | null = null;
  private fileInput: HTMLInputElement | null = null;
  private scale: number = 1;
  private drawing = false;

  constructor(os: OS) {
    super(os);
  }

  protected getApplicationName(): string {
    return 'hack-paint';
  }

  protected initApplication(): void {
    if (!this.container) return;
    this.render();
    this.setupEvents();
  }

  private render(): void {
    if (!this.container) return;
    this.container.innerHTML = `
      <div class="hack-paint-toolbar">
        <button class="open-btn">Open</button>
        <button class="save-btn">Save</button>
        <button class="zoom-in-btn">Zoom In</button>
        <button class="zoom-out-btn">Zoom Out</button>
        <input type="color" class="color-input" value="#000000" />
        <input type="range" class="size-input" min="1" max="50" value="5" />
        <input type="file" class="file-input" accept="image/*" style="display:none;" />
      </div>
      <div class="hack-paint-canvas-container">
        <canvas class="hack-paint-canvas" width="800" height="600"></canvas>
      </div>
    `;
    this.canvas = this.container.querySelector('.hack-paint-canvas');
    this.colorInput = this.container.querySelector('.color-input');
    this.sizeInput = this.container.querySelector('.size-input');
    this.fileInput = this.container.querySelector('.file-input');
    if (this.canvas) this.ctx = this.canvas.getContext('2d');
  }

  private setupEvents(): void {
    if (!this.container || !this.canvas || !this.ctx) return;
    const openBtn = this.container.querySelector('.open-btn');
    const saveBtn = this.container.querySelector('.save-btn');
    const zoomInBtn = this.container.querySelector('.zoom-in-btn');
    const zoomOutBtn = this.container.querySelector('.zoom-out-btn');

    openBtn?.addEventListener('click', () => this.fileInput?.click());
    this.fileInput?.addEventListener('change', (e) => this.loadImage(e));
    saveBtn?.addEventListener('click', () => this.saveImage());
    zoomInBtn?.addEventListener('click', () => this.zoom(1.25));
    zoomOutBtn?.addEventListener('click', () => this.zoom(0.8));

    this.canvas.addEventListener('mousedown', (e) => this.startDraw(e));
    this.canvas.addEventListener('mousemove', (e) => this.draw(e));
    this.canvas.addEventListener('mouseup', () => this.stopDraw());
    this.canvas.addEventListener('mouseleave', () => this.stopDraw());
  }

  private loadImage(e: Event): void {
    const input = e.target as HTMLInputElement;
    if (!input.files || input.files.length === 0 || !this.ctx) return;
    const file = input.files[0];
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        if (!this.canvas) return;
        this.canvas.width = img.width;
        this.canvas.height = img.height;
        this.ctx!.drawImage(img, 0, 0);
      };
      if (typeof reader.result === 'string') {
        img.src = reader.result;
      }
    };
    reader.readAsDataURL(file);
  }

  private saveImage(): void {
    if (!this.canvas) return;
    const url = this.canvas.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = url;
    a.download = 'image.png';
    a.click();
  }

  private zoom(factor: number): void {
    if (!this.canvas) return;
    this.scale *= factor;
    this.canvas.style.transform = `scale(${this.scale})`;
  }

  private startDraw(e: MouseEvent): void {
    if (!this.ctx || !this.canvas) return;
    this.drawing = true;
    this.ctx.beginPath();
    const rect = this.canvas.getBoundingClientRect();
    this.ctx.moveTo((e.clientX - rect.left) / this.scale, (e.clientY - rect.top) / this.scale);
  }

  private draw(e: MouseEvent): void {
    if (!this.drawing || !this.ctx || !this.canvas) return;
    const rect = this.canvas.getBoundingClientRect();
    const color = this.colorInput?.value || '#000000';
    const size = parseInt(this.sizeInput?.value || '5', 10);
    this.ctx.strokeStyle = color;
    this.ctx.lineWidth = size;
    this.ctx.lineCap = 'round';
    this.ctx.lineTo((e.clientX - rect.left) / this.scale, (e.clientY - rect.top) / this.scale);
    this.ctx.stroke();
  }

  private stopDraw(): void {
    if (!this.drawing || !this.ctx) return;
    this.drawing = false;
    this.ctx.closePath();
  }
}
