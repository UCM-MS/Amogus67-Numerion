CREATE TABLE `mp_world_players` (
	`world_code` text NOT NULL,
	`player_id` text NOT NULL,
	`position_json` text NOT NULL,
	`pet_json` text NOT NULL,
	`last_seen` integer NOT NULL,
	PRIMARY KEY(`world_code`, `player_id`),
	FOREIGN KEY (`world_code`) REFERENCES `mp_worlds`(`code`) ON UPDATE no action ON DELETE cascade,
	FOREIGN KEY (`player_id`) REFERENCES `mp_players`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE INDEX `mp_world_members_presence` ON `mp_world_players` (`world_code`,`last_seen`);--> statement-breakpoint
CREATE INDEX `mp_world_members_player` ON `mp_world_players` (`player_id`);--> statement-breakpoint
CREATE TABLE `mp_world_rewards` (
	`world_code` text NOT NULL,
	`player_id` text NOT NULL,
	`amount` integer DEFAULT 60 NOT NULL,
	`applied` integer DEFAULT 0 NOT NULL,
	`created_at` integer NOT NULL,
	PRIMARY KEY(`world_code`, `player_id`),
	FOREIGN KEY (`player_id`) REFERENCES `mp_players`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE INDEX `mp_world_rewards_player` ON `mp_world_rewards` (`player_id`);--> statement-breakpoint
CREATE TABLE `mp_worlds` (
	`code` text PRIMARY KEY NOT NULL,
	`state_json` text NOT NULL,
	`version` integer DEFAULT 0 NOT NULL,
	`host_id` text NOT NULL,
	`created_at` integer NOT NULL,
	`updated_at` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `mp_worlds_updated_at` ON `mp_worlds` (`updated_at`);--> statement-breakpoint
ALTER TABLE `mp_players` ADD `active_world` text;