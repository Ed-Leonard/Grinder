export function portal(
  node: HTMLElement,
  target: string | HTMLElement = "body",
) {
  const targetEl =
    typeof target === "string" ? document.querySelector(target) : target;

  targetEl?.appendChild(node);

  return {
    destroy() {
      node.remove();
    },
  };
}
