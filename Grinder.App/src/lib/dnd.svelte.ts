import { type DragDropState } from "@thisux/sveltednd";

// Used for the undo/redo stacks
// Contains all required information to restore the tasks to previous state, even for multiple lists
export type OrderAction = {
  itemId: number;
  fromListId: number;
  fromIndex: number;
  toListId: number;
  toIndex: number;
};

// Used to store the currently dragged task
type ActiveDrag = {
  itemId: number;
  originalListId: number;
  originalIndex: number;
  currentListId: number;
  lastTargetId: number;
  lastPosition: string;
};

// Little helper function that inserts to the startt of a list if empty. Or inserts to specific location based on the library exposed properties + target container
function resolveInsertIndex<T>(
  items: T[],
  targetItemId: number,
  position: string,
  getItemId: (t: T) => number,
): number | null {
  if (targetItemId === 0) return items.length;
  const idx = items.findIndex((t) => getItemId(t) === targetItemId);
  if (idx === -1) return null;
  // "after" is exposed by the library to determine if the cursor is more than haldfway through the container it is being dropped on
  return position === "after" ? idx + 1 : idx;
}

const UNDO_LIMIT = 20;

// This defines the constructor arguments that are passed from the component/page
export type DndConfig<TItem> = {
  // This is required because we store list-id and task-id to update state, for the board view this will be different since the different 'lists' are statuses not task lists
  parseContainer(container: string): { listId: number; itemId: number } | null;

  // literally just returns the task id
  getItemId(item: TItem): number;

  // This is a conditional boolean which is used to determine if a item can be dropped on a container. For single lists this means something like targetlist.id === initialList.id etc. for a kanban board this would simply be true
  canDrop?(from: ActiveDrag, targetListId: number): boolean;

  // This allows the page to determine what updates on drop. So for tasks page we update the task order in the back end. For board page we update the status.
  onCommit?(action: OrderAction): void;
};

export class DndManager<TItem> {
  // bit of a state dump
  #items = $state<Record<number, TItem[]>>({});

  // This is used to provide updates to the view while dragging which is not default behaviour with this library
  // In general updating the items list messes with the DOM which breaks events
  #viewItems = $state<Record<number, TItem[]> | null>(null);

  #ghostItemId = $state<number | null>(null);
  #undoStack = $state<OrderAction[]>([]);
  #redoStack = $state<OrderAction[]>([]);

  // this is for the toast message
  #hitLimit = $state(false);
  #activeDrag: ActiveDrag | null = null;
  #cfg: DndConfig<TItem>;

  // Build the config for the page, this can be added to by modifying DndConfig interface to make it suit more needs
  constructor(config: DndConfig<TItem>) {
    this.#cfg = config;
  }

  // Getters for the page to handle error messages
  // In general we don't want the dnd manager to handle page specific stuff
  get canUndo() {
    return this.#undoStack.length > 0;
  }

  get canRedo() {
    return this.#redoStack.length > 0;
  }

  get hitLimit() {
    return this.#hitLimit;
  }

  get ghostItemId() {
    return this.#ghostItemId;
  }

  getItems(listId: number): TItem[] {
    return this.#items[listId] ?? [];
  }

  // mainly used to style the ghost class using tailwind
  // reduces opacity so it doesn't look like user is just duplicating task when moving it
  isGhost(listId: number, itemId: number): boolean {
    return (
      this.#ghostItemId === itemId &&
      this.#activeDrag?.originalListId === listId
    );
  }

  getViewItems(listId: number): TItem[] {
    return this.#viewItems?.[listId] ?? this.#items[listId] ?? [];
  }

  setItems(listId: number, items: TItem[]) {
    this.#items = { ...this.#items, [listId]: items };
  }

  // called only when task is actually dropped and preserves state as well as updating undo/redo stacks
  commit(action: OrderAction) {
    const next = [...this.#undoStack, action];
    if (next.length > UNDO_LIMIT) this.#hitLimit = true;
    this.#undoStack = next.slice(-UNDO_LIMIT);
    this.#redoStack = [];
    this.#cfg.onCommit?.(action);
  }

  // undo and redo are very similar just opposite actions taken so we just pass arguements into generic function backwards for redo
  undo(): OrderAction | null {
    return this.#applyHistory(this.#undoStack, this.#redoStack, true);
  }

  redo(): OrderAction | null {
    return this.#applyHistory(this.#redoStack, this.#undoStack, false);
  }

  // Takes two stacks that contain order actions and steps back or forward through the task state by applying the top action, for undo it is applied in reverse
  #applyHistory(
    stack: OrderAction[],
    opposite: OrderAction[],
    reverse: boolean,
  ): OrderAction | null {
    const action = stack.at(-1);
    if (!action) return null;

    const fromListId = reverse ? action.toListId : action.fromListId;
    const toListId = reverse ? action.fromListId : action.toListId;
    const fromIndex = reverse ? action.toIndex : action.fromIndex;
    const toIndex = reverse ? action.fromIndex : action.toIndex;

    const sourceItems = [...this.getItems(fromListId)];
    const [item] = sourceItems.splice(fromIndex, 1);
    if (!item) return null;

    // If item has moved across a list the need to update both lists
    if (fromListId === toListId) {
      sourceItems.splice(toIndex, 0, item);
      this.#items = { ...this.#items, [fromListId]: sourceItems };
    } else {
      const targetItems = [...this.getItems(toListId)];
      targetItems.splice(toIndex, 0, item);
      this.#items = {
        ...this.#items,
        [fromListId]: sourceItems,
        [toListId]: targetItems,
      };
    }

    if (reverse) {
      this.#undoStack = stack.slice(0, -1);
      this.#redoStack = [...opposite, action];
    } else {
      this.#redoStack = stack.slice(0, -1);
      this.#undoStack = [...opposite, action];
    }

    return action;
  }

  // Called when a user begins dragging task
  // Sets up the active drag, view items, and ghost item on drag over and end handle all logic
  handleDragStart = (state: DragDropState<TItem>) => {
    const parsed = this.#cfg.parseContainer(state.sourceContainer);
    if (!parsed) return;

    const itemId = this.#cfg.getItemId(state.draggedItem);
    const items = this.getItems(parsed.listId);
    const index = items.findIndex((t) => this.#cfg.getItemId(t) === itemId);

    this.#viewItems = { ...this.#items };
    this.#ghostItemId = null;

    this.#activeDrag = {
      itemId,
      originalListId: parsed.listId,
      originalIndex: index,
      currentListId: parsed.listId,
      lastTargetId: -1,
      lastPosition: "",
    };
  };

  // This is a biggy but it handles a range of scenarios
  // 1. Same list reorder is fairly straight forward
  // 2. Dragging from an initial list to another
  // 3. Draging from a list back to the initial list
  // 4. Dragging from 2 lists, neither which are the initial one
  // All scenarios need to be handled slightly differently to properly display the ghost item etc.
  handleDragOver = (state: DragDropState<TItem>) => {
    // prevents running function if cursor is nowhere interesting
    if (!this.#activeDrag || !state.targetContainer || !state.dropPosition)
      return;

    const parsed = this.#cfg.parseContainer(state.targetContainer);
    if (!parsed) return;

    // Prevents running function on every drag if the container is the same and the position inside the container has not gone over halfway
    if (
      parsed.itemId === this.#activeDrag.lastTargetId &&
      state.dropPosition === this.#activeDrag.lastPosition
    )
      return;

    // This is what enforces the drop conditions
    if (
      this.#cfg.canDrop &&
      !this.#cfg.canDrop(this.#activeDrag, parsed.listId)
    )
      return;

    const { itemId, originalListId, currentListId } = this.#activeDrag;
    const targetListId = parsed.listId;
    const view = this.#viewItems!;

    if (currentListId === targetListId) {
      // Same-list reorder
      // Finds new index and moves item from original index to new index
      // We ONLY update view items which means no DOM manipulation shenanigans
      const items = [...(view[targetListId] ?? [])];
      const fromIndex = items.findIndex(
        (t) => this.#cfg.getItemId(t) === itemId,
      );
      if (fromIndex === -1) return;

      const [item] = items.splice(fromIndex, 1);
      const insertAt = resolveInsertIndex(
        items,
        parsed.itemId,
        state.dropPosition,
        this.#cfg.getItemId,
      );
      if (insertAt === null) return;
      items.splice(insertAt, 0, item);

      this.#viewItems = { ...view, [targetListId]: items };
    } else {
      const newView = { ...view };

      // In all 3 scenarios we need to remove move item into new list
      // In the case of original to new list we dont remove item we just set it as a ghost
      // And in the case of returning home we need to remove the ghost
      const draggedItem =
        currentListId === originalListId
          ? view[originalListId]?.find((t) => this.#cfg.getItemId(t) === itemId)
          : view[currentListId]?.find((t) => this.#cfg.getItemId(t) === itemId);
      if (!draggedItem) return;

      // Remove from current — unless it's the original list (ghost must stay in DOM)
      if (currentListId !== originalListId) {
        const sourceItems = [...(view[currentListId] ?? [])];
        const idx = sourceItems.findIndex(
          (t) => this.#cfg.getItemId(t) === itemId,
        );
        if (idx === -1) return;
        sourceItems.splice(idx, 1);
        newView[currentListId] = sourceItems;
      }

      // Insert into target
      const targetItems = [...(view[targetListId] ?? [])];

      let toIndex: number | null;
      if (targetListId === originalListId) {
        const ghostIdx = targetItems.findIndex(
          (t) => this.#cfg.getItemId(t) === itemId,
        );
        if (ghostIdx === -1) return;
        targetItems.splice(ghostIdx, 1);

        toIndex =
          parsed.itemId === itemId
            ? ghostIdx
            : resolveInsertIndex(
                targetItems,
                parsed.itemId,
                state.dropPosition,
                this.#cfg.getItemId,
              );
      } else {
        toIndex = resolveInsertIndex(
          targetItems,
          parsed.itemId,
          state.dropPosition,
          this.#cfg.getItemId,
        );
      }
      if (toIndex === null) return;
      targetItems.splice(toIndex, 0, draggedItem);
      newView[targetListId] = targetItems;

      this.#ghostItemId = targetListId === originalListId ? null : itemId;

      this.#viewItems = newView;
    }

    this.#activeDrag = {
      ...this.#activeDrag,
      currentListId: targetListId,
      lastTargetId: parsed.itemId,
      lastPosition: state.dropPosition,
    };
  };

  // This is where we actually update the state
  // Change the DOM and send the appropriate request to back end with new state
  handleDragEnd = () => {
    if (!this.#activeDrag) return;

    const { itemId, originalListId, originalIndex, currentListId } =
      this.#activeDrag;

    const view = this.#viewItems!;
    const destItems = view[currentListId] ?? [];
    const toIndex = destItems.findIndex(
      (t) => this.#cfg.getItemId(t) === itemId,
    );

    // This is the only place where we actually touch the items list
    if (
      toIndex !== -1 &&
      (originalListId !== currentListId || originalIndex !== toIndex)
    ) {
      if (originalListId !== currentListId) {
        // Remove ghost from original list if it exists, finalise in current
        const sourceItems = (view[originalListId] ?? []).filter(
          (t) => this.#cfg.getItemId(t) !== itemId,
        );
        this.#items = {
          ...this.#items,
          [originalListId]: sourceItems,
          [currentListId]: destItems,
        };
      } else {
        this.#items = { ...this.#items, [currentListId]: destItems };
      }
      this.commit({
        itemId,
        fromListId: originalListId,
        fromIndex: originalIndex,
        toListId: currentListId,
        toIndex,
      });
    }
    // If dropped in an invalid location (toIndex === -1) or same position,
    // #items stays unchanged — the view reset below restores the original appearance.

    this.#viewItems = null;
    this.#ghostItemId = null;
    this.#activeDrag = null;
  };
}
