import { useId, type FormEvent, type ReactNode } from 'react'

/** A Bootstrap modal that holds a form. The footer holds the buttons, and the close button calls onClose. */
export function Modal({
  title,
  onClose,
  onSubmit,
  footer,
  children,
}: {
  title: string
  onClose: () => void
  onSubmit: (event: FormEvent) => void
  footer: ReactNode
  children: ReactNode
}) {
  const titleId = useId()
  return (
    <>
      <div className="modal d-block" role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <div className="modal-dialog modal-dialog-centered">
          <form className="modal-content" onSubmit={onSubmit}>
            <div className="modal-header">
              <h2 className="modal-title fs-5" id={titleId}>
                {title}
              </h2>
              <button type="button" className="btn-close" aria-label="Close" onClick={onClose} />
            </div>
            <div className="modal-body">{children}</div>
            <div className="modal-footer">{footer}</div>
          </form>
        </div>
      </div>
      <div className="modal-backdrop show" />
    </>
  )
}
