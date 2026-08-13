import { vi } from "vitest";
import { act, render, screen } from "@testing-library/react";
import SafeProductImage from "./safeproductimage";

describe("SafeProductImage", () => {
  let resizeCallback;
  let pendingFrames;
  let originalImage;
  let originalResizeObserver;
  let originalRequestAnimationFrame;
  let originalCancelAnimationFrame;
  let originalGetContext;
  let clientWidthDescriptor;
  let clientHeightDescriptor;

  const observe = vi.fn();
  const disconnect = vi.fn();
  const context = {
    setTransform: vi.fn(),
    fillRect: vi.fn(),
    drawImage: vi.fn(),
    getImageData: vi.fn(() => ({ data: new Uint8ClampedArray(4) })),
    putImageData: vi.fn(),
  };

  beforeEach(() => {
    pendingFrames = [];
    resizeCallback = null;
    observe.mockClear();
    disconnect.mockClear();
    Object.values(context).forEach((mock) => mock?.mockClear?.());

    originalImage = window.Image;
    originalResizeObserver = window.ResizeObserver;
    originalRequestAnimationFrame = window.requestAnimationFrame;
    originalCancelAnimationFrame = window.cancelAnimationFrame;
    originalGetContext = HTMLCanvasElement.prototype.getContext;
    clientWidthDescriptor = Object.getOwnPropertyDescriptor(
      HTMLCanvasElement.prototype,
      "clientWidth"
    );
    clientHeightDescriptor = Object.getOwnPropertyDescriptor(
      HTMLCanvasElement.prototype,
      "clientHeight"
    );

    class ImageMock {
      constructor() {
        this.naturalWidth = 640;
        this.naturalHeight = 480;
        this.onload = null;
      }

      set src(value) {
        this.currentSrc = value;
        if (value) this.onload?.();
      }

      get src() {
        return this.currentSrc || "";
      }
    }

    class ResizeObserverMock {
      constructor(callback) {
        resizeCallback = callback;
      }

      observe(target) {
        observe(target);
      }

      disconnect() {
        disconnect();
      }
    }

    window.Image = ImageMock;
    window.ResizeObserver = ResizeObserverMock;
    global.Image = ImageMock;
    global.ResizeObserver = ResizeObserverMock;
    window.requestAnimationFrame = vi.fn((callback) => {
      pendingFrames.push(callback);
      return pendingFrames.length;
    });
    window.cancelAnimationFrame = vi.fn();
    HTMLCanvasElement.prototype.getContext = vi.fn(() => context);
    Object.defineProperty(HTMLCanvasElement.prototype, "clientWidth", {
      configurable: true,
      get: () => 320,
    });
    Object.defineProperty(HTMLCanvasElement.prototype, "clientHeight", {
      configurable: true,
      get: () => 180,
    });
  });

  afterEach(() => {
    window.Image = originalImage;
    window.ResizeObserver = originalResizeObserver;
    global.Image = originalImage;
    global.ResizeObserver = originalResizeObserver;
    window.requestAnimationFrame = originalRequestAnimationFrame;
    window.cancelAnimationFrame = originalCancelAnimationFrame;
    HTMLCanvasElement.prototype.getContext = originalGetContext;

    if (clientWidthDescriptor) {
      Object.defineProperty(
        HTMLCanvasElement.prototype,
        "clientWidth",
        clientWidthDescriptor
      );
    } else {
      delete HTMLCanvasElement.prototype.clientWidth;
    }

    if (clientHeightDescriptor) {
      Object.defineProperty(
        HTMLCanvasElement.prototype,
        "clientHeight",
        clientHeightDescriptor
      );
    } else {
      delete HTMLCanvasElement.prototype.clientHeight;
    }
  });

  it("observes the stable parent and draws outside the resize callback", () => {
    const { unmount } = render(
      <div className="image-frame" data-testid="image-frame">
        <SafeProductImage src="/product.webp" alt="Product" />
      </div>
    );
    const canvas = screen.getByRole("img", { name: "Product" });
    const imageFrame = screen.getByTestId("image-frame");

    expect(observe).toHaveBeenCalledWith(imageFrame);
    expect(observe).not.toHaveBeenCalledWith(canvas);
    expect(context.drawImage).not.toHaveBeenCalled();

    act(() => {
      pendingFrames.shift()();
    });

    expect(canvas.width).toBe(320);
    expect(canvas.height).toBe(180);
    expect(context.drawImage).toHaveBeenCalledTimes(1);

    act(() => {
      resizeCallback([]);
    });

    expect(context.drawImage).toHaveBeenCalledTimes(1);
    expect(pendingFrames).toHaveLength(1);

    act(() => {
      pendingFrames.shift()();
    });

    expect(context.drawImage).toHaveBeenCalledTimes(2);
    unmount();
    expect(disconnect).toHaveBeenCalledTimes(1);
  });

  it("clears the previous canvas pixels when the next product has no image", () => {
    const { rerender } = render(
      <div className="image-frame">
        <SafeProductImage src="/product.webp" alt="Product" />
      </div>
    );

    act(() => {
      pendingFrames.shift()();
    });
    expect(context.drawImage).toHaveBeenCalledTimes(1);

    context.fillRect.mockClear();
    context.drawImage.mockClear();
    rerender(
      <div className="image-frame">
        <SafeProductImage src="" alt="Product without image" />
      </div>
    );

    expect(context.fillRect).toHaveBeenCalledTimes(1);
    expect(context.drawImage).not.toHaveBeenCalled();
  });
});
