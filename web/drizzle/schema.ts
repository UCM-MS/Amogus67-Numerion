import { sqliteTable, text, integer, index, primaryKey } from 'drizzle-orm/sqlite-core';

export const multiplayerPlayers = sqliteTable('mp_players', {
  id: text('id').primaryKey(),
  secretHash: text('secret_hash').notNull(),
  name: text('name').notNull(),
  rating: integer('rating').notNull().default(1000),
  wins: integer('wins').notNull().default(0),
  matches: integer('matches').notNull().default(0),
  shards: integer('shards').notNull().default(0),
  power: integer('power').notNull().default(0),
  shield: integer('shield').notNull().default(0),
  focus: integer('focus').notNull().default(0),
  activeRoom: text('active_room'),
  activeWorld: text('active_world'),
  createdAt: integer('created_at').notNull(),
  lastSeen: integer('last_seen').notNull(),
}, table => [index('mp_players_ranking').on(table.rating, table.wins), index('mp_players_active_room').on(table.activeRoom)]);

export const multiplayerRooms = sqliteTable('mp_rooms', {
  code: text('code').primaryKey(),
  stateJson: text('state_json').notNull(),
  revision: integer('revision').notNull().default(0),
  createdAt: integer('created_at').notNull(),
  updatedAt: integer('updated_at').notNull(),
}, table => [index('mp_rooms_updated_at').on(table.updatedAt)]);

export const multiplayerMatchResults = sqliteTable('mp_match_results', {
  roomCode: text('room_code').notNull(),
  playerId: text('player_id').notNull().references(() => multiplayerPlayers.id),
  ratingDelta: integer('rating_delta').notNull(),
  shards: integer('shards').notNull(),
  winner: integer('winner').notNull().default(0),
  applied: integer('applied').notNull().default(0),
  createdAt: integer('created_at').notNull(),
}, table => [primaryKey({ columns: [table.roomCode, table.playerId] }), index('mp_results_player').on(table.playerId)]);

export const sharedWorlds = sqliteTable('mp_worlds', {
  code: text('code').primaryKey(),
  stateJson: text('state_json').notNull(),
  version: integer('version').notNull().default(0),
  hostId: text('host_id').notNull(),
  createdAt: integer('created_at').notNull(),
  updatedAt: integer('updated_at').notNull(),
}, table => [index('mp_worlds_updated_at').on(table.updatedAt)]);

export const sharedWorldPlayers = sqliteTable('mp_world_players', {
  worldCode: text('world_code').notNull().references(() => sharedWorlds.code, { onDelete: 'cascade' }),
  playerId: text('player_id').notNull().references(() => multiplayerPlayers.id),
  positionJson: text('position_json').notNull(),
  petJson: text('pet_json').notNull(),
  lastSeen: integer('last_seen').notNull(),
}, table => [primaryKey({ columns: [table.worldCode, table.playerId] }), index('mp_world_members_presence').on(table.worldCode, table.lastSeen), index('mp_world_members_player').on(table.playerId)]);

export const sharedWorldRewards = sqliteTable('mp_world_rewards', {
  worldCode: text('world_code').notNull(),
  playerId: text('player_id').notNull().references(() => multiplayerPlayers.id),
  amount: integer('amount').notNull().default(60),
  applied: integer('applied').notNull().default(0),
  createdAt: integer('created_at').notNull(),
}, table => [primaryKey({ columns: [table.worldCode, table.playerId] }), index('mp_world_rewards_player').on(table.playerId)]);

export const unityProfiles = sqliteTable('unity_profiles', {
  id: text('id').primaryKey(), secretHash: text('secret_hash').notNull(), saveJson: text('save_json').notNull(),
  revision: integer('revision').notNull().default(1), activeRoom: text('active_room'),
  createdAt: integer('created_at').notNull(), lastSeen: integer('last_seen').notNull(),
}, table => [index('unity_profiles_created').on(table.createdAt)]);
export const unityRooms = sqliteTable('unity_rooms', {
  code: text('code').primaryKey(), beaconsMask:integer('beacons_mask').notNull().default(0),createdAt: integer('created_at').notNull(), updatedAt: integer('updated_at').notNull(),
}, table => [index('unity_rooms_updated').on(table.updatedAt)]);
export const unityRoomMembers = sqliteTable('unity_room_members', {
  roomCode: text('room_code').notNull().references(() => unityRooms.code, {onDelete:'cascade'}),
  playerId: text('player_id').notNull().references(() => unityProfiles.id, {onDelete:'cascade'}),
  presenceJson: text('presence_json').notNull(), sequence: integer('sequence').notNull().default(0), lastSeen: integer('last_seen').notNull(),
}, table => [primaryKey({columns:[table.roomCode,table.playerId]}),index('unity_members_player').on(table.playerId)]);
export const unityBattles = sqliteTable('unity_battles', {
  id: text('id').primaryKey(),roomCode:text('room_code').notNull().references(()=>unityRooms.code,{onDelete:'cascade'}),
  leftId:text('left_id').notNull(),rightId:text('right_id').notNull(),status:text('status').notNull(),stateJson:text('state_json').notNull(),
  revision:integer('revision').notNull().default(0),createdAt:integer('created_at').notNull(),updatedAt:integer('updated_at').notNull(),
},table=>[index('unity_battles_members').on(table.roomCode,table.leftId,table.rightId,table.status)]);
export const unityBattleRewards=sqliteTable('unity_battle_rewards',{
  battleId:text('battle_id').notNull(),playerId:text('player_id').notNull().references(()=>unityProfiles.id,{onDelete:'cascade'}),
  xp:integer('xp').notNull(),coins:integer('coins').notNull(),petXp:integer('pet_xp').notNull(),createdAt:integer('created_at').notNull(),
},table=>[primaryKey({columns:[table.battleId,table.playerId]})]);
