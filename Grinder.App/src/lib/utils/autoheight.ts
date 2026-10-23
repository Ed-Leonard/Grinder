export default function autoHeight(node: HTMLElement) {
  let resizeObserver;

  function update() {
    const height = node.scrollHeight;
    node.parentElement
      ? (node.parentElement.style.height = height + "px")
      : null;
  }

  resizeObserver = new ResizeObserver(update);
  resizeObserver.observe(node);
  update();

  return {
    destroy() {
      resizeObserver.disconnect();
    },
  };
}
