import * as signalR from "@microsoft/signalr";
import { notificationStore } from "$lib/stores/notifications";
import { resolve } from "$app/paths";

let connection: signalR.HubConnection | null = null;

// Create web socket connection with notifications hub on back end
export function connectNotificationHub() {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    .withUrl(resolve("/api/hubs/notifications" as any), {
      withCredentials: true,
    })
    .withAutomaticReconnect()
    .build();

  connection.on("ReceiveNotification", (n) => notificationStore.prepend(n));
  connection.on("ReceiveUnreadCount", (c: number) =>
    notificationStore.setUnreadCount(c),
  );

  connection.start().catch(console.error);
  return connection;
}

export async function stopNotifications() {
  await connection?.stop();
}
