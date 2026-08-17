// Multi monitor management utilities

/**
 * Interface for monitor messages exchanged over BroadcastChannel
 */
export interface MonitorMessage<T = any> {
  type: string;
  id?: number;
  data?: T;
}

/**
 * Manager class for handling additional monitor windows and
 * communication between them using BroadcastChannel.
 */
export class MultiMonitorManager extends EventTarget {
  private channel: BroadcastChannel;
  private monitors: Map<number, Window> = new Map();
  private connected: Set<number> = new Set();
  private channelName: string;
  public isSecondary: boolean = false;

  constructor(channelName: string = 'multi-monitor') {
    super();
    this.channelName = channelName;
    this.channel = new BroadcastChannel(this.channelName);
    this.channel.addEventListener('message', (e) => this.handleMessage(e));

    const params = new URLSearchParams(window.location.search);
    const monitorId = params.get('monitor');
    if (monitorId !== null) {
      const id = parseInt(monitorId, 10);
      this.channel.postMessage({ type: 'handshake', id } as MonitorMessage);
    }
  }

  public connectAsSecondary(): void {
    this.isSecondary = true;
    const params = new URLSearchParams(window.location.search);
    const monitorId = params.get('monitor');
    if (monitorId) {
      const id = parseInt(monitorId, 10);
      this.channel.postMessage({ type: 'handshake', id } as MonitorMessage);
    }
  }

  /**
   * Handle incoming messages on the BroadcastChannel
   */
  private handleMessage(event: MessageEvent): void {
    const msg = event.data as MonitorMessage;
    if (!msg || typeof msg !== 'object') return;

    switch (msg.type) {
      case 'handshake':
        if (typeof msg.id === 'number') {
          this.connected.add(msg.id);
        }
        break;
      case 'close':
        if (typeof msg.id === 'number') {
          this.connected.delete(msg.id);
          this.monitors.delete(msg.id);
        }
        break;
      case 'message':
        this.dispatchEvent(new CustomEvent('message', { detail: msg }));
        break;
    }
  }

  /**
   * Open a new monitor window with the given id
   */
  public openMonitor(id: number, url: string = window.location.href): void {
    const monitorUrl = new URL(url, window.location.href);
    monitorUrl.searchParams.set('monitor', id.toString());
    const win = window.open(monitorUrl.toString(), `monitor-${id}`);
    if (win) {
      this.monitors.set(id, win);
    } else {
      console.error('Failed to open monitor window');
    }
  }

  /**
   * Send a message to a specific monitor via BroadcastChannel
   */
  public sendMessage<T = any>(id: number, data: T): void {
    const message: MonitorMessage<T> = { type: 'message', id, data };
    this.channel.postMessage(message);
  }

  /**
   * Check if a monitor window is connected
   */
  public isConnected(id: number): boolean {
    return this.connected.has(id);
  }

  /**
   * Close a monitor window by id
   */
  public closeMonitor(id: number): void {
    const win = this.monitors.get(id);
    if (win) {
      win.close();
      this.channel.postMessage({ type: 'close', id } as MonitorMessage);
    }
    this.monitors.delete(id);
    this.connected.delete(id);
  }
}

