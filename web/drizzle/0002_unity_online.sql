CREATE TABLE unity_profiles (
  id TEXT PRIMARY KEY NOT NULL,
  secret_hash TEXT NOT NULL,
  save_json TEXT NOT NULL,
  revision INTEGER NOT NULL DEFAULT 1,
  active_room TEXT,
  created_at INTEGER NOT NULL,
  last_seen INTEGER NOT NULL
);
--> statement-breakpoint
CREATE INDEX unity_profiles_created ON unity_profiles(created_at);
--> statement-breakpoint
CREATE TABLE unity_rooms (
  code TEXT PRIMARY KEY NOT NULL,
  beacons_mask INTEGER NOT NULL DEFAULT 0,
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL
);
--> statement-breakpoint
CREATE INDEX unity_rooms_updated ON unity_rooms(updated_at);
--> statement-breakpoint
CREATE TABLE unity_room_members (
  room_code TEXT NOT NULL REFERENCES unity_rooms(code) ON DELETE CASCADE,
  player_id TEXT NOT NULL REFERENCES unity_profiles(id) ON DELETE CASCADE,
  presence_json TEXT NOT NULL,
  sequence INTEGER NOT NULL DEFAULT 0,
  last_seen INTEGER NOT NULL,
  PRIMARY KEY(room_code, player_id)
);
--> statement-breakpoint
CREATE INDEX unity_members_player ON unity_room_members(player_id);
--> statement-breakpoint
CREATE TABLE unity_battles (
  id TEXT PRIMARY KEY NOT NULL,
  room_code TEXT NOT NULL REFERENCES unity_rooms(code) ON DELETE CASCADE,
  left_id TEXT NOT NULL,
  right_id TEXT NOT NULL,
  status TEXT NOT NULL,
  state_json TEXT NOT NULL,
  revision INTEGER NOT NULL DEFAULT 0,
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL
);
--> statement-breakpoint
CREATE INDEX unity_battles_members ON unity_battles(room_code, left_id, right_id, status);
--> statement-breakpoint
CREATE TABLE unity_battle_rewards (
  battle_id TEXT NOT NULL,
  player_id TEXT NOT NULL REFERENCES unity_profiles(id) ON DELETE CASCADE,
  xp INTEGER NOT NULL,
  coins INTEGER NOT NULL,
  pet_xp INTEGER NOT NULL,
  created_at INTEGER NOT NULL,
  PRIMARY KEY(battle_id, player_id)
);
