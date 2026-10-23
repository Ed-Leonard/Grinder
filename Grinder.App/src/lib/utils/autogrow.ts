export default function autoGrow(node: HTMLTextAreaElement, _value?: string) {
  const resize = () => {
    node.style.height = "auto";
    node.style.height = node.scrollHeight + "px";
  };
  node.addEventListener("input", resize);
  const observer = new ResizeObserver(() => resize());
  observer.observe(node);
  resize();
  return {
    update: () => resize(),
    destroy: () => {
      node.removeEventListener("input", resize);
      observer.disconnect();
    },
  };
}
