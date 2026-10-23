import { browser } from "$app/environment";

export function getUserLocale() {
  return browser ? navigator.language : "en-NZ";
}

export function formatTimer(totalSeconds: number) {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  const pad = (n: number) => n.toString().padStart(2, "0");
  return h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

export function localisedDateTimePattern(locale = getUserLocale()): string {
  const dtf = new Intl.DateTimeFormat(locale, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });

  const parts = dtf.formatToParts(new Date(Date.UTC(2000, 11, 31, 13, 45)));

  return parts
    .map((p) => {
      switch (p.type) {
        case "day":
          return "dd";
        case "month":
          return "MM";
        case "year":
          return "yyyy";
        case "hour":
          return "HH";
        case "minute":
          return "mm";
        case "literal":
          return p.value;
        default:
          return "";
      }
    })
    .join("")
    .replace(/\s+/g, " ")
    .trim();
}

export function formatDate(
  date: string,
  time?: string | null,
  locale = getUserLocale(),
): string {
  const formatted = new Date(`${date}T00:00:00`).toLocaleDateString(locale);
  if (!time) return formatted;
  const formattedTime = new Date(`1970-01-01T${time}`).toLocaleTimeString(
    locale,
    { hour: "2-digit", minute: "2-digit" },
  );
  return `${formatted} at ${formattedTime}`;
}

export function formatDateTime(date: string, locale = getUserLocale()): string {
  return new Date(date).toLocaleString(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export function timeAgo(
  dateInput: Date | string,
  locale = getUserLocale(),
): string {
  const date = typeof dateInput === "string" ? new Date(dateInput) : dateInput;
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);

  if (seconds < 60) return "Just now";

  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "always" });
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return rtf.format(-minutes, "minute");

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return rtf.format(-hours, "hour");

  const days = Math.floor(hours / 24);
  return rtf.format(-days, "day");
}

export function shortTimeAgo(dateInput: string): string {
  const seconds = Math.floor(
    (Date.now() - new Date(dateInput).getTime()) / 1000,
  );
  if (seconds < 60) return "now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d`;
  return `${Math.floor(days / 7)}w`;
}

export function getLocalisedDateFormat(): string {
  const parts = new Intl.DateTimeFormat(navigator.language).formatToParts(
    new Date(2025, 11, 31),
  );
  return parts
    .map((p) => {
      if (p.type === "year") return "YYYY";
      if (p.type === "month") return "MM";
      if (p.type === "day") return "DD";
      return p.value;
    })
    .join("");
}

export function validateField(
  input: HTMLInputElement | null,
  getMessage: () => string | null,
) {
  if (!input) return;
  input.setCustomValidity(getMessage() ?? "");
}

type DateTimeAccessors = {
  getDate: () => string;
  getTime: () => string | null;
  setTime: (value: string) => void;
};

export function createDueDateTimeValidator({
  getDate,
  getTime,
  setTime,
}: DateTimeAccessors) {
  let dueDateError = $state("");
  let dueTimeError = $state("");

  function getDueDateError(): string | null {
    const date = getDate();
    if (!date) return "Due date is required";
    const due = new Date(date + "T00:00:00");
    if (Number.isNaN(due.getTime())) return "Invalid due date";
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (due < today) return "Invalid due date, date must be in the future";
    return null;
  }

  function getDueTimeError(): string | null {
    const time = getTime();
    const date = getDate();
    if (!time) return null;
    if (!date) return "Due time cannot be set without a due date";
    const combined = new Date(`${date}T${time}`);
    if (Number.isNaN(combined.getTime())) return "Invalid due time";
    if (combined < new Date()) return "Due time must be in the future";
    return null;
  }

  function validate(
    dueDateInput: HTMLInputElement | null,
    dueTimeInput: HTMLInputElement | null,
  ) {
    if (!getDate() && !dueDateInput?.validity.badInput) setTime("");
    dueDateError = getDueDateError() ?? "";
    dueTimeError = getDueTimeError() ?? "";
    validateField(dueDateInput, getDueDateError);
    validateField(dueTimeInput, getDueTimeError);
  }

  return {
    get dueDateError() {
      return dueDateError;
    },
    get dueTimeError() {
      return dueTimeError;
    },
    validate,
  };
}

type StartDateTimeAccessors = {
  getStartDate: () => string | null;
  getStartTime: () => string | null;
  setStartTime: (value: string) => void;
  getDueDate: () => string;
  getDueTime: () => string | null;
};

export function createStartDateTimeValidator({
  getStartDate,
  getStartTime,
  setStartTime,
  getDueDate,
  getDueTime,
}: StartDateTimeAccessors) {
  let startDateError = $state("");
  let startTimeError = $state("");

  function getStartDateError(): string | null {
    const startDate = getStartDate();
    if (!startDate) return null;
    const start = new Date(startDate + "T00:00:00");
    if (Number.isNaN(start.getTime())) return "Invalid start date";
    // Only reject if the date is before the current day, allow if it's later the same day
    const due = new Date(`${getDueDate()}T${getDueTime() || "23:59"}`);
    if (start > due) return "Invalid start date, date must be before due date";
    return null;
  }

  function getStartTimeError(): string | null {
    const startTime = getStartTime();
    const startDate = getStartDate();
    if (!startTime) return null;
    if (!startDate) return "Start time cannot be set without a start date";
    const combined = new Date(`${startDate}T${startTime}`);
    if (Number.isNaN(combined.getTime())) return "Invalid start time";
    const due = new Date(`${getDueDate()}T${getDueTime() || "23:59"}`);
    if (combined >= due) return "Start time must be before due time";
    return null;
  }

  function validate(
    startDateInput: HTMLInputElement | null,
    startTimeInput: HTMLInputElement | null,
  ) {
    if (!getStartDate() && !startDateInput?.validity.badInput) setStartTime("");
    startDateError = getStartDateError() ?? "";
    startTimeError = getStartTimeError() ?? "";
    validateField(startDateInput, getStartDateError);
    validateField(startTimeInput, getStartTimeError);
  }

  return {
    get startDateError() {
      return startDateError;
    },
    get startTimeError() {
      return startTimeError;
    },
    validate,
  };
}
