"use client";

import { useEffect, useRef, type ReactNode } from "react";

/** Native <dialog> modal: focus trap, Esc to close, backdrop click to close. */
export function Modal({
  open,
  onClose,
  title,
  palette,
  children,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  palette?: string;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const d = ref.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      data-palette={palette}
      onClose={onClose}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      onClick={(e) => {
        if (e.target === ref.current) onClose();
      }}
      aria-label={title}
      className="px-panel m-auto w-[calc(100%-32px)] max-w-[560px] bg-surface-1 p-0 text-text backdrop:bg-[rgb(7_8_11/0.8)] backdrop:backdrop-blur-[2px]"
      data-accent="true"
    >
      {open && (
        <div className="px-rise max-h-[85dvh] overflow-y-auto p-4 sm:p-6">
          <div className="mb-4 flex items-start justify-between gap-4">
            <h2 className="text-[20px] leading-6 text-text">{title}</h2>
            <button onClick={onClose} aria-label="Close" className="px-border -mt-1 grid size-10 shrink-0 place-items-center bg-surface-2 font-pixel text-muted hover:text-text">
              X
            </button>
          </div>
          {children}
        </div>
      )}
    </dialog>
  );
}
