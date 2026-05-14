create table if not exists trash_bin_state_latest (
  simulation_id text not null,
  bin_id text not null,
  x double precision not null,
  y double precision not null,
  z double precision not null,
  is_full boolean not null default false,
  fill_ratio double precision not null default 0,
  current_amount integer not null default 0,
  capacity integer not null default 0,
  updated_at timestamptz not null default now(),
  primary key (simulation_id, bin_id)
);

create table if not exists trash_truck_state_latest (
  simulation_id text not null,
  truck_id text not null,
  x double precision not null,
  y double precision not null,
  z double precision not null,
  collected_count integer not null default 0,
  max_load integer not null default 0,
  status text not null check (status in ('idle', 'collecting', 'returning')),
  updated_at timestamptz not null default now(),
  primary key (simulation_id, truck_id)
);

alter table trash_bin_state_latest enable row level security;
alter table trash_truck_state_latest enable row level security;

-- Capstone/demo policy: anon clients can read and upsert telemetry rows.
-- Tighten this later if user auth is added.
create policy "anon can read bin telemetry"
  on trash_bin_state_latest for select
  to anon
  using (true);

create policy "anon can insert bin telemetry"
  on trash_bin_state_latest for insert
  to anon
  with check (true);

create policy "anon can update bin telemetry"
  on trash_bin_state_latest for update
  to anon
  using (true)
  with check (true);

create policy "anon can read truck telemetry"
  on trash_truck_state_latest for select
  to anon
  using (true);

create policy "anon can insert truck telemetry"
  on trash_truck_state_latest for insert
  to anon
  with check (true);

create policy "anon can update truck telemetry"
  on trash_truck_state_latest for update
  to anon
  using (true)
  with check (true);
