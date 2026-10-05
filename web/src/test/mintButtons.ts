export const mintButtons = () =>
  [...document.querySelectorAll<HTMLElement>('[data-slot="button"]')].filter((button) =>
    button.classList.contains('bg-action'),
  );
