# Supabase Telemetry Setup

## Supabase

1. Supabase SQL editor에서 `Docs/supabase_telemetry_schema.sql`을 실행한다.
2. Unity 담당자에게 아래 값만 공유한다.
   - Project URL
   - anon public key

`service_role` key는 Unity/WebGL 클라이언트에 넣지 않는다.

## Unity

1. 씬에 빈 GameObject를 만든다. 예: `SupabaseTelemetry`
2. 같은 GameObject에 아래 컴포넌트를 추가한다.
   - `SupabaseTelemetryUploader`
   - `SimulationTelemetryReporter`
3. `SupabaseTelemetryUploader`에 Supabase 값을 입력한다.
   - `Supabase Url`: `https://xxxxx.supabase.co`
   - `Anon Key`: Supabase anon public key
4. 기본 업로드 주기는 `SimulationTelemetryReporter.uploadIntervalSeconds = 1`초다.

## Uploaded Tables

`trash_bin_state_latest`

- `simulation_id`
- `bin_id`
- `x`, `y`, `z`
- `is_full`
- `fill_ratio`
- `current_amount`
- `capacity`
- `updated_at`

`trash_truck_state_latest`

- `simulation_id`
- `truck_id`
- `x`, `y`, `z`
- `collected_count`
- `max_load`
- `status`
- `updated_at`

## Notes

- `simulation_id`는 실행마다 자동 생성된다.
- 쓰레기통 ID는 grid position이 있으면 `bin_x_y` 형식으로 생성된다.
- 수거차 ID는 GameObject 이름을 기반으로 생성된다.
- REST upsert 기준 conflict key는 각각 `(simulation_id, bin_id)`, `(simulation_id, truck_id)`다.
