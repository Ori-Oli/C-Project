# Supabase Telemetry Setup

## Supabase

1. Supabase SQL editor에서 `Docs/supabase_telemetry_schema.sql`을 실행한다.
2. Unity 담당자에게 아래 값만 공유한다.
   - Project URL
   - anon public key

`service_role` key는 Unity/WebGL 클라이언트에 넣지 않는다.
`DATABASE_URL`도 Unity/WebGL 클라이언트에 넣지 않는다.

## Unity

1. 프로젝트 루트의 `.env.example`을 참고해서 로컬 `.env`를 만든다.
   - `SUPABASE_URL`
   - `SUPABASE_ANON_KEY`
2. 씬에 빈 GameObject를 만든다. 예: `SupabaseTelemetry`
3. 같은 GameObject에 아래 컴포넌트를 추가한다.
   - `SupabaseTelemetryUploader`
   - `SimulationTelemetryReporter`
4. 기본 업로드 주기는 `SimulationTelemetryReporter.uploadIntervalSeconds = 1`초다.

`SupabaseTelemetryUploader`는 Play Mode/Standalone에서 `.env`를 자동으로 읽는다. WebGL 배포 빌드는 브라우저 런타임에서 로컬 `.env`를 읽을 수 없으므로, 빌드용 씬/프리팹에는 `Supabase Url`과 `Anon Key` 값을 인스펙터에 넣거나 별도 배포 설정으로 주입해야 한다.

## Uploaded Tables

`trash_bin_state_latest`

- `simulation_id`
- `bin_id`
- `x`, `y`, `z`
- `is_full`
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
