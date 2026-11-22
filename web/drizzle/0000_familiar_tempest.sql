CREATE TABLE `mp_match_results` (
	`room_code` text NOT NULL,
	`player_id` text NOT NULL,
	`rating_delta` integer NOT NULL,
	`shards` integer NOT NULL,
	`winner` integer DEFAULT 0 NOT NULL,
	`applied` integer DEFAULT 0 NOT NULL,
	`created_at` integer NOT NULL,
	PRIMARY KEY(`room_code`, `player_id`),
	FOREIGN KEY (`player_id`) REFERENCES `mp_players`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE INDEX `mp_results_player` ON `mp_match_results` (`player_id`);--> statement-breakpoint
CREATE TABLE `mp_players` (
	`id` text PRIMARY KEY NOT NULL,
	`secret_hash` text NOT NULL,
	`name` text NOT NULL,
	`rating` integer DEFAULT 1000 NOT NULL,
	`wins` integer DEFAULT 0 NOT NULL,
	`matches` integer DEFAULT 0 NOT NULL,
	`shards` integer DEFAULT 0 NOT NULL,
	`power` integer DEFAULT 0 NOT NULL,
	`shield` integer DEFAULT 0 NOT NULL,
	`focus` integer DEFAULT 0 NOT NULL,
	`active_room` text,
	`created_at` integer NOT NULL,
	`last_seen` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `mp_players_ranking` ON `mp_players` (`rating`,`wins`);--> statement-breakpoint
CREATE INDEX `mp_players_active_room` ON `mp_players` (`active_room`);--> statement-breakpoint
CREATE TABLE `mp_rooms` (
	`code` text PRIMARY KEY NOT NULL,
	`state_json` text NOT NULL,
	`revision` integer DEFAULT 0 NOT NULL,
	`created_at` integer NOT NULL,
	`updated_at` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `mp_rooms_updated_at` ON `mp_rooms` (`updated_at`);