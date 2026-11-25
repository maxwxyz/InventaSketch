import { describe, expect, it } from "vitest";
import { isPictureName, scaledSize, toBase64 } from "../src/ui/picture";
import { portTipKey } from "../src/ui/buildlogic";

describe("charts from pictures (web)", () => {
  it("recognises picture files", () => {
    expect(isPictureName("chart.PNG")).toBe(true);
    expect(isPictureName("scan.jpeg")).toBe(true);
    expect(isPictureName("data.csv")).toBe(false);
    expect(isPictureName("drawing.svg")).toBe(false);
  });

  it("scales large pictures down, never up", () => {
    expect(scaledSize(3200, 1600)).toEqual({ w: 1600, h: 800 });
    expect(scaledSize(400, 300)).toEqual({ w: 400, h: 300 });
  });

  it("encodes large byte arrays in chunks", () => {
    const bytes = new Uint8Array(100000).map((_, i) => i % 256);
    const b64 = toBase64(bytes);
    expect(atob(b64).length).toBe(100000);
    expect(atob(b64).charCodeAt(99999)).toBe(99999 % 256);
  });
});

describe("connection \"+\" tooltips", () => {
  it("names connection ports and line ends", () => {
    const port = { id: "p", x: 0, y: 0, dx: 1, dy: 0, stretch: false };
    expect(portTipKey({ ...port, kind: "Connect" })).toBe("port.Connect");
    expect(portTipKey({ ...port, kind: "LineEnd" })).toBe("port.LineEnd");
    expect(portTipKey({ ...port, kind: "Unknown" })).toBe("port.other");
  });
});
