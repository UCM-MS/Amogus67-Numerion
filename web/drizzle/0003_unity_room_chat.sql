CREATE TABLE unity_room_messages (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  room_code TEXT NOT NULL REFERENCES unity_rooms(code) ON DELETE CASCADE,
  author_id TEXT NOT NULL REFERENCES unity_profiles(id) ON DELETE CASCADE,
  author_name TEXT NOT NULL,
  text TEXT NOT NULL DEFAULT '',
  emote_id TEXT NOT NULL DEFAULT '',
  client_id TEXT NOT NULL,
  created_at INTEGER NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX unity_chat_client_dedup ON unity_room_messages(room_code, author_id, client_id);
--> statement-breakpoint
CREATE INDEX unity_chat_room_cursor ON unity_room_messages(room_code, id);
--> statement-breakpoint
CREATE INDEX unity_chat_expiry ON unity_room_messages(created_at);
--> statement-breakpoint
CREATE TABLE unity_chat_limits (
  room_code TEXT NOT NULL REFERENCES unity_rooms(code) ON DELETE CASCADE,
  player_id TEXT NOT NULL REFERENCES unity_profiles(id) ON DELETE CASCADE,
  last_at INTEGER NOT NULL,
  window_at INTEGER NOT NULL,
  window_count INTEGER NOT NULL,
  nonce TEXT NOT NULL,
  PRIMARY KEY(room_code, player_id)
);
--> statement-breakpoint
CREATE INDEX unity_chat_limits_expiry ON unity_chat_limits(last_at);
