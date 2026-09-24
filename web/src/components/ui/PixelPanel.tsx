import type { ElementType, HTMLAttributes, ReactNode } from "react";

interface PixelPanelProps extends HTMLAttributes<HTMLElement> {
  as?: ElementType;
  accent?: boolean;
  palette?: string;
  padded?: boolean;
  children: ReactNode;
}

/** Surface with a notched 2px pixel border and a hard drop shadow. No rounded corners. */
export function PixelPanel({ as: Tag = "div", accent = false, palette, padded = true, className = "", children, ...rest }: PixelPanelProps) {
  return (
    <Tag className={`px-panel ${padded ? "p-4" : ""} ${className}`} data-accent={accent ? "true" : undefined} data-palette={palette} {...rest}>
      {children}
    </Tag>
  );
}

export function SectionTitle({ kicker, title, className = "" }: { kicker?: string; title: string; className?: string }) {
  return (
    <div className={`mb-4 ${className}`}>
      {kicker && <p className="font-pixel text-[12px] leading-4 text-accent">{kicker}</p>}
      <h2 className="text-[24px] leading-8 text-text sm:text-[32px] sm:leading-10">{title}</h2>
    </div>
  );
}
