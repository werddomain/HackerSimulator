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
  private exportFormat: HTMLSelectElement | null = null;
  private qualityInput: HTMLInputElement | null = null;
  private canvasContainer: HTMLElement | null = null;
  private scale: number = 1;
  private drawing = false;
  private panMode = false;
  private panning = false;
  private panStartX = 0;
  private panStartY = 0;
  private scrollLeftStart = 0;
  private scrollTopStart = 0;
  private cropMode = false;
  private selecting = false;
  private selectionRect: HTMLDivElement | null = null;
  private selStartX = 0;
  private selStartY = 0;
  private history: ImageData[] = [];
  private historyIndex = -1;

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
    this.pushHistory();
  }

  private render(): void {
    if (!this.container) return;
    this.container.innerHTML = `
      <div class="hack-paint-toolbar">
        <button class="new-btn">New</button>
        <button class="open-btn">Open</button>
        <button class="save-btn">Save</button>
        <button class="export-btn">Export</button>
        <button class="undo-btn">Undo</button>
        <button class="redo-btn">Redo</button>
        <button class="rotate90-btn">Rotate 90°</button>
        <button class="crop-btn">Crop</button>
        <button class="grid-btn">Grid</button>
        <button class="pan-btn">Pan</button>
        <button class="zoom-in-btn">Zoom In</button>
        <button class="zoom-out-btn">Zoom Out</button>
        <select class="export-format">
          <option value="png">PNG</option>
          <option value="jpeg">JPEG</option>
        </select>
        <input type="range" class="quality-input" min="0" max="1" step="0.1" value="0.92" />
        <input type="color" class="color-input" value="#000000" />
        <input type="range" class="size-input" min="1" max="50" value="5" />
        <input type="file" class="file-input" accept="image/*" style="display:none;" />
      </div>
      <div class="hack-paint-canvas-container">
        <canvas class="hack-paint-canvas" width="800" height="600"></canvas>
      </div>
    `;
    this.canvas = this.container.querySelector('.hack-paint-canvas');
    this.canvasContainer = this.container.querySelector('.hack-paint-canvas-container');
    this.colorInput = this.container.querySelector('.color-input');
    this.sizeInput = this.container.querySelector('.size-input');
    this.fileInput = this.container.querySelector('.file-input');
    this.exportFormat = this.container.querySelector('.export-format');
    this.qualityInput = this.container.querySelector('.quality-input');
    if (this.canvas) this.ctx = this.canvas.getContext('2d');
  }

  private setupEvents(): void {
    if (!this.container || !this.canvas || !this.ctx) return;
    const newBtn = this.container.querySelector('.new-btn');
    const openBtn = this.container.querySelector('.open-btn');
    const saveBtn = this.container.querySelector('.save-btn');
    const exportBtn = this.container.querySelector('.export-btn');
    const undoBtn = this.container.querySelector('.undo-btn');
    const redoBtn = this.container.querySelector('.redo-btn');
    const rotate90Btn = this.container.querySelector('.rotate90-btn');
    const cropBtn = this.container.querySelector('.crop-btn');
    const gridBtn = this.container.querySelector('.grid-btn');
    const panBtn = this.container.querySelector('.pan-btn');
    const zoomInBtn = this.container.querySelector('.zoom-in-btn');
    const zoomOutBtn = this.container.querySelector('.zoom-out-btn');

    newBtn?.addEventListener('click', () => this.newDocument());
    openBtn?.addEventListener('click', () => this.fileInput?.click());
    this.fileInput?.addEventListener('change', (e) => this.loadImage(e));
    saveBtn?.addEventListener('click', () => this.saveImage());
    exportBtn?.addEventListener('click', () => this.exportImage());
    undoBtn?.addEventListener('click', () => this.undo());
    redoBtn?.addEventListener('click', () => this.redo());
    rotate90Btn?.addEventListener('click', () => this.rotate90());
    cropBtn?.addEventListener('click', () => this.toggleCropMode());
    gridBtn?.addEventListener('click', () => this.toggleGrid());
    panBtn?.addEventListener('click', () => this.togglePanMode());
    zoomInBtn?.addEventListener('click', () => this.zoom(1.25));
    zoomOutBtn?.addEventListener('click', () => this.zoom(0.8));

    this.canvas.addEventListener('mousedown', (e) => this.handleMouseDown(e));
    this.canvas.addEventListener('mousemove', (e) => this.handleMouseMove(e));
    this.canvas.addEventListener('mouseup', (e) => this.handleMouseUp(e));
    this.canvas.addEventListener('mouseleave', () => this.handleMouseUp());

    this.canvasContainer?.addEventListener('mousedown', (e) => this.startPan(e));
    this.canvasContainer?.addEventListener('mousemove', (e) => this.pan(e));
    this.canvasContainer?.addEventListener('mouseup', () => this.stopPan());
    this.canvasContainer?.addEventListener('mouseleave', () => this.stopPan());
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
        this.pushHistory();
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
    this.pushHistory();
  }

  private newDocument(): void {
    if (!this.canvas || !this.ctx) return;
    const width = parseInt(prompt('Width', '800') || '800', 10);
    const height = parseInt(prompt('Height', '600') || '600', 10);
    const color = prompt('Background color (empty for transparent)', '#ffffff');
    this.canvas.width = width;
    this.canvas.height = height;
    if (color) {
      this.ctx.fillStyle = color;
      this.ctx.fillRect(0, 0, width, height);
    } else {
      this.ctx.clearRect(0, 0, width, height);
    }
    this.pushHistory();
  }

  private exportImage(): void {
    if (!this.canvas) return;
    const format = this.exportFormat?.value || 'png';
    const quality = parseFloat(this.qualityInput?.value || '0.92');
    let mime = 'image/png';
    if (format === 'jpeg') mime = 'image/jpeg';
    const url = this.canvas.toDataURL(mime, quality);
    const a = document.createElement('a');
    a.href = url;
    a.download = `image.${format}`;
    a.click();
  }

  private rotate90(): void {
    if (!this.canvas || !this.ctx) return;
    const temp = document.createElement('canvas');
    temp.width = this.canvas.width;
    temp.height = this.canvas.height;
    const tctx = temp.getContext('2d');
    if (!tctx) return;
    tctx.drawImage(this.canvas, 0, 0);
    this.canvas.width = temp.height;
    this.canvas.height = temp.width;
    this.ctx.save();
    this.ctx.translate(this.canvas.width, 0);
    this.ctx.rotate(Math.PI / 2);
    this.ctx.drawImage(temp, 0, 0);
    this.ctx.restore();
    this.pushHistory();
  }

  private toggleGrid(): void {
    if (!this.canvasContainer) return;
    this.canvasContainer.classList.toggle('grid');
  }

  private togglePanMode(): void {
    this.panMode = !this.panMode;
  }

  private startPan(e: MouseEvent): void {
    if (!this.panMode) return;
    this.panning = true;
    this.panStartX = e.clientX;
    this.panStartY = e.clientY;
    if (this.canvasContainer) {
      this.scrollLeftStart = this.canvasContainer.scrollLeft;
      this.scrollTopStart = this.canvasContainer.scrollTop;
    }
  }

  private pan(e: MouseEvent): void {
    if (!this.panning || !this.canvasContainer) return;
    this.canvasContainer.scrollLeft = this.scrollLeftStart - (e.clientX - this.panStartX);
    this.canvasContainer.scrollTop = this.scrollTopStart - (e.clientY - this.panStartY);
  }

  private stopPan(): void {
    this.panning = false;
  }

  private toggleCropMode(): void {
    this.cropMode = !this.cropMode;
    if (this.cropMode && !this.selectionRect && this.canvasContainer) {
      this.selectionRect = document.createElement('div');
      this.selectionRect.className = 'selection-rect';
      this.canvasContainer.appendChild(this.selectionRect);
    }
    if (this.selectionRect) {
      this.selectionRect.style.display = this.cropMode ? 'block' : 'none';
    }
  }

  private handleMouseDown(e: MouseEvent): void {
    if (this.panMode) {
      return;
    }
    if (this.cropMode) {
      this.startSelection(e);
    } else {
      this.startDraw(e);
    }
  }

  private handleMouseMove(e: MouseEvent): void {
    if (this.panMode) {
      return;
    }
    if (this.cropMode) {
      this.updateSelection(e);
    } else {
      this.draw(e);
    }
  }

  private handleMouseUp(e?: MouseEvent): void {
    if (this.panMode) {
      return;
    }
    if (this.cropMode) {
      this.finishSelection(e);
    } else {
      this.stopDraw();
    }
  }

  private startSelection(e: MouseEvent): void {
    if (!this.cropMode || !this.canvas || !this.selectionRect) return;
    this.selecting = true;
    const rect = this.canvas.getBoundingClientRect();
    this.selStartX = (e.clientX - rect.left) / this.scale;
    this.selStartY = (e.clientY - rect.top) / this.scale;
    this.selectionRect.style.left = `${e.clientX - rect.left}px`;
    this.selectionRect.style.top = `${e.clientY - rect.top}px`;
    this.selectionRect.style.width = '0px';
    this.selectionRect.style.height = '0px';
  }

  private updateSelection(e: MouseEvent): void {
    if (!this.selecting || !this.selectionRect || !this.canvas) return;
    const rect = this.canvas.getBoundingClientRect();
    const currentX = e.clientX - rect.left;
    const currentY = e.clientY - rect.top;
    const width = currentX - parseFloat(this.selectionRect.style.left || '0');
    const height = currentY - parseFloat(this.selectionRect.style.top || '0');
    this.selectionRect.style.width = `${Math.abs(width)}px`;
    this.selectionRect.style.height = `${Math.abs(height)}px`;
    if (width < 0) this.selectionRect.style.left = `${currentX}px`;
    if (height < 0) this.selectionRect.style.top = `${currentY}px`;
  }

  private finishSelection(e?: MouseEvent): void {
    if (!this.selecting || !this.canvas || !this.ctx || !this.selectionRect) return;
    this.selecting = false;
    const rect = this.canvas.getBoundingClientRect();
    const endX = e ? (e.clientX - rect.left) / this.scale : this.selStartX;
    const endY = e ? (e.clientY - rect.top) / this.scale : this.selStartY;
    const x = Math.min(this.selStartX, endX);
    const y = Math.min(this.selStartY, endY);
    const w = Math.abs(endX - this.selStartX);
    const h = Math.abs(endY - this.selStartY);
    if (w > 0 && h > 0) {
      const data = this.ctx.getImageData(x, y, w, h);
      this.canvas.width = w;
      this.canvas.height = h;
      this.ctx.putImageData(data, 0, 0);
      this.pushHistory();
    }
    this.toggleCropMode();
  }

  private pushHistory(): void {
    if (!this.canvas || !this.ctx) return;
    const data = this.ctx.getImageData(0, 0, this.canvas.width, this.canvas.height);
    this.history = this.history.slice(0, this.historyIndex + 1);
    this.history.push(data);
    this.historyIndex = this.history.length - 1;
  }

  private undo(): void {
    if (!this.canvas || !this.ctx) return;
    if (this.historyIndex > 0) {
      this.historyIndex--;
      const data = this.history[this.historyIndex];
      this.canvas.width = data.width;
      this.canvas.height = data.height;
      this.ctx.putImageData(data, 0, 0);
    }
  }

  private redo(): void {
    if (!this.canvas || !this.ctx) return;
    if (this.historyIndex < this.history.length - 1) {
      this.historyIndex++;
      const data = this.history[this.historyIndex];
      this.canvas.width = data.width;
      this.canvas.height = data.height;
      this.ctx.putImageData(data, 0, 0);
    }
  }
}
