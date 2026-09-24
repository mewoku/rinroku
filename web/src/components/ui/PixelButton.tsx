import Link from "next/link";
import type { ButtonHTMLAttributes, ReactNode } from "react";

type Variant = "primary" | "secondary" | "ghost";
type Size = "sm" | "md" | "lg";

interface Common {
  variant?: Variant;
  size?: Size;
  /** Override palette for this button, e.g. "boss" or "pattern". */
  palette?: string;
  className?: string;
  children: ReactNode;
}

type AsButton = Common & ButtonHTMLAttributes<HTMLButtonElement> & { href?: undefined };
type AsLink = Common & { href: string; external?: boolean; "aria-label"?: string };

/** Glow button: gradient accent→accent2, dithered halo, pixel border, 2px pressed shift. */
export function PixelButton(props: AsButton | AsLink) {
  const { variant = "primary", size = "md", palette, className = "", children } = props;
  const shared = {
    className: `px-btn ${className}`,
    "data-variant": variant,
    "data-size": size,
    "data-palette": palette,
  };
  if (props.href !== undefined) {
    const { href, external } = props as AsLink;
    if (external) {
      return (
        <a href={href} target="_blank" rel="noopener noreferrer" aria-label={props["aria-label"]} {...shared}>
          {children}
        </a>
      );
    }
    return (
      <Link href={href} aria-label={props["aria-label"]} {...shared}>
        {children}
      </Link>
    );
  }
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { variant: _v, size: _s, palette: _p, className: _c, children: _ch, type, ...rest } = props as AsButton;
  return (
    <button type={type ?? "button"} {...rest} {...shared}>
      {children}
    </button>
  );
}
