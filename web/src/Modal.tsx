import { useId, type FormEvent, type ReactNode } from 'react'
import { useModalFocus } from './useModalFocus'

/**
 * A Bootstrap modal that holds a form. The footer holds the buttons, and the close button and the Escape key call onClose, unless closeDisabled is set.
 * Focus moves into the modal, stays there, and returns afterwards.
 */
export function Modal({
  title,
  onClose,
  closeDisabled = false,
  onSubmit,
  footer,
  children,
}: {
  title: string
  onClose: () => void
  closeDisabled?: boolean
  onSubmit: (event: FormEvent) => void
  footer: ReactNode
  children: ReactNode
}) {
  const titleId = useId()
  const dialog = useModalFocus<HTMLDivElement>(() => {
    if (!closeDisabled) onClose()
  })
  return (
    <>
      <div
        ref={dialog}
        tabIndex={-1}
        className="modal d-block"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <div className="modal-dialog modal-dialog-centered">
          <form className="modal-content" onSubmit={onSubmit}>
            <div className="modal-header">
              <h2 className="modal-title fs-5" id={titleId}>
                {title}
              </h2>
              <button
                type="button"
                className="btn-close"
                aria-label="Close"
                disabled={closeDisabled}
                onClick={onClose}
              />
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
