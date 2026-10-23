import type { WrappedExtraStats } from "$lib/api/wrapped";

export type StatKey =
  | "badgesEarned"
  | "friendsMade"
  | "newFollowers"
  | "messagesSent"
  | "sharedTasks"
  | "commentsWritten"
  | "reactionsReceived"
  | "tasksOverdue";

export const STAT_LABELS: Record<StatKey, string> = {
  badgesEarned: "badges earned",
  friendsMade: "friends made",
  newFollowers: "new followers",
  messagesSent: "messages sent",
  sharedTasks: "tasks shared",
  commentsWritten: "comments written",
  reactionsReceived: "reactions received",
  tasksOverdue: "tasks overdue",
};

export function getStatValue(stats: WrappedExtraStats, key: StatKey): number {
  switch (key) {
    case "badgesEarned":
      return stats.badgesEarnedCount;
    case "friendsMade":
      return stats.numberOfFriendsMade;
    case "newFollowers":
      return stats.numberOfNewFollowers;
    case "messagesSent":
      return stats.numberOfMessagesSent;
    case "sharedTasks":
      return stats.numberOfSharedTasks;
    case "commentsWritten":
      return stats.numberOfCommentsWritten;
    case "reactionsReceived":
      return stats.numberOfReactionsReceived;
    case "tasksOverdue":
      return stats.numberOfTasksOverdue;
  }
}

export const STAT_MSG: Record<StatKey, (value: number) => string> = {
  badgesEarned: (v) =>
    v >= 10
      ? "your trophy case called, it's full"
      : v >= 5
        ? "certified badge collector"
        : "just getting started",
  friendsMade: (v) =>
    v >= 10
      ? "you're basically running a social club"
      : v >= 4
        ? "building your crew"
        : "made a friend! a start",
  newFollowers: (v) =>
    v >= 50
      ? "influencer era, unlocked"
      : v >= 15
        ? "growing an audience"
        : "slowly but surely",
  messagesSent: (v) =>
    v >= 200
      ? "chronically online"
      : v >= 75
        ? "never leaves people on read"
        : "kept in touch",
  sharedTasks: (v) =>
    v >= 20
      ? "delegation champion"
      : v >= 8
        ? "team player"
        : "shared the load",
  commentsWritten: (v) =>
    v >= 30
      ? "unofficial group chat narrator"
      : v >= 10
        ? "always has an opinion"
        : "dropped a few thoughts",
  reactionsReceived: (v) =>
    v >= 75
      ? "certified crowd pleaser"
      : v >= 25
        ? "crowd favorite"
        : "got some love",
  tasksOverdue: (v) =>
    v >= 20
      ? "living in your own timezone"
      : v >= 5
        ? "fashionably late"
        : "a little behind, no big deal",
};

// Only worth showing once the number's big enough for "a month" to say anything —
// "about 0 a month" for a stat of 3 would undercut it rather than add context.
function buildRateLine(value: number): string | null {
  if (value < 12) return null;
  const perMonth = Math.round(value / 12);
  return perMonth <= 1
    ? "about 1 a month, on average"
    : `about ${perMonth} a month, on average`;
}

export type ExtraStatSlideData = {
  id: string;
  rank: number;
  label: string;
  value: number;
  message: string;
  rateLine: string | null;
  otherHighlights: { label: string; value: number }[];
};

// Ranks the non-zero extra stats and returns the top `count`.
// tasksOverdue is excluded from ranking — it's not something to hold up as a "top" stat.
export function buildTopExtraStatSlides(
  stats: WrappedExtraStats | null | undefined,
  count = 3,
): ExtraStatSlideData[] {
  if (!stats) return [];

  const ranked = (Object.keys(STAT_LABELS) as StatKey[])
    .filter((key) => key !== "tasksOverdue")
    .map((key) => ({
      id: key,
      label: STAT_LABELS[key],
      value: getStatValue(stats, key),
    }))
    .filter((s) => s.value > 0)
    .sort((a, b) => b.value - a.value)
    .slice(0, count);

  return ranked.map((s, i) => ({
    id: s.id,
    rank: i + 1,
    label: s.label,
    value: s.value,
    message: STAT_MSG[s.id as StatKey](s.value),
    rateLine: buildRateLine(s.value),
    otherHighlights: ranked
      .filter((_, j) => j !== i)
      .map(({ label, value }) => ({ label, value })),
  }));
}
