import { useEffect, useRef } from 'react'

const FOCUSABLE =
  'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])'

/**
 * Makes a dialog behave for keyboard users. Focus moves into the dialog when it opens, Tab stays inside it,
 * Escape asks to close it, and focus goes back to where it was when the dialog closes. Bootstrap's modal
 * script does this, but the app uses only Bootstrap's CSS.
 * @param onClose Called when the user presses Escape.
 * @returns A ref for the element that holds the dialog.
 */
export function useModalFocus<T extends HTMLElement>(onClose: () => void) {
  const dialog = useRef<T>(null)
  const close = useRef(onClose)
  useEffect(() => {
    close.current = onClose
  })

  useEffect(() => {
    const element = dialog.current
    const opener = document.activeElement as HTMLElement | null
    const focusable = () => [...(element?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [])]
    ;(focusable()[0] ?? element)?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') return close.current()
      if (event.key !== 'Tab') return
      const items = focusable()
      if (items.length === 0) return event.preventDefault()
      const first = items[0]
      const last = items[items.length - 1]
      const outside = !element?.contains(document.activeElement)
      if (event.shiftKey && (document.activeElement === first || outside)) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && (document.activeElement === last || outside)) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      opener?.focus()
    }
  }, [])

  return dialog
}
