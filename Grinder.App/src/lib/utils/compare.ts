function diffObjects<T extends object>(current: T, initial: T): Partial<T> {
  return (Object.keys(current) as (keyof T)[]).reduce((patch, key) => {
    const a = current[key];
    const b = initial[key];

    const changed =
      Array.isArray(a) && Array.isArray(b)
        ? a.length !== b.length || a.some((v, i) => v !== b[i])
        : a !== b;

    if (changed) patch[key] = current[key];
    return patch;
  }, {} as Partial<T>);
}

export { diffObjects };
