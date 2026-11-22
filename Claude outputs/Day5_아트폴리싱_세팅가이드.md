# 포텐더 Day 5 — 아트 & 폴리싱 세팅 가이드

스크립트는 레포에 들어가 있음: `Assets/01. Potender/10. Scripts/Potender/`
- `Polish/` 신규 3개: **ModelSwap**(캡슐 손님·고블린 → Synty 캐릭터), **FpsCounter**(시야 왼쪽 아래 FPS + Logcat 기록), **QuestFoveation**(고정 포비티드 렌더링)
- `Editor/PotenderDay5Setup.cs` 신규 — 메뉴 3개
  - **Potender ▸ Day5 폴리싱 세팅** — 캐릭터 · 조명 · 성능 · UI · 효과음 · 오디오 압축 (여러 번 실행해도 됨)
  - **Potender ▸ Day5 방 꾸미기 (Synty)** — 레스토랑 홀: 정면 문 · 테이블 · 샹들리에 · 뒤 선반 (실행할 때마다 `Day5_Environment`를 **새로** 만듦)
  - **Potender ▸ Day5 방 꾸미기 되돌리기** — `Day5_Environment` 삭제 + Plane · 입구/출구/고블린 스폰 위치 복구
- 수정: `Goblin.cs` — Synty 모델도 초록색 + 피격 번쩍 대상에 포함 (Init에 몇 줄 추가)

> ⚠ 실행 전에 **git commit 먼저** (씬·프리팹·URP 에셋을 건드림). 결과가 마음에 안 들면 `git checkout -- .` 로 바로 복구

## 0. 오늘 순서 (컷: 18:00 이후 기능 추가 X, 버그 수정·연출만)

| 순서 | 할 일 | 완료 확인 |
|---|---|---|
| 1 | git commit → Unity 컴파일 → **Day5 폴리싱 세팅** | 팝업에 결과 목록, Console 에러 없음 |
| 2 | Play → Enter | 손님이 Synty 캐릭터로 걸어옴 · 왼쪽 아래 FPS |
| 3 | R (습격) | 고블린이 **초록 Synty 캐릭터** · G로 맞히면 주황 번쩍 |
| 4 | **Day5 방 꾸미기** (선택) | 정면에 문, 넓은 홀에 테이블, 손님이 문으로 들어옴 |
| 5 | Scene 뷰에서 위치 손보기 | 손님 동선·메뉴판·UI가 벽/테이블에 안 가림 |
| 6 | **Quest 빌드 → FPS 확인** | 평소 70+ / 습격 중에도 60+ |
| 7 | BGM · 효과음 파일 넣고 메뉴 재실행 | 영업 시작 종 · 동전 · 틀림 소리 |
| 8 | UI 정리 (아래 5번 체크리스트) | 1.5m 거리에서 글자 읽힘 |
| 마감 | 최종 빌드 + 녹화 (FpsCounter `show` 끄기) | 영업 → 습격 퇴치 → 정산 한 판 |

**우선순위 (시간 없으면 위에서부터)**: 프레임 72 > 캐릭터 교체 > 조명 > BGM/효과음 > UI 정리 > 방 꾸미기

## 1. 폴리싱 세팅 결과 (메뉴 실행하면 생기는 것)

| 항목 | 내용 |
|---|---|
| **Customer 프리팹** | ModelSwap 추가 — Synty FantasyKingdom 캐릭터 11종 중 랜덤 (농부·상인·대장장이·마법사·수도사·점술가·병사·기사), 키 1.7m, 캡슐·머리·코 숨김 |
| **Goblin 프리팹** | ModelSwap — `SM_Chr_Jester_01`(어릿광대)를 초록으로 칠해서 고블린, 키 1.25m |
| 캐릭터 공통 | 모델 콜라이더 끔 (판정은 원래 콜라이더) · 그림자 끔 · 걸을 때 통통 튀는 흔들림 (애니메이션 대신) |
| **Directional Light** | 기본값일 때만: 따뜻한 색 · 0.7 · **그림자 끔** / Baked면 Mixed로 |
| **주변광** | Source = Color, 따뜻한 갈색 (습격 때 RaidEvent가 붉게 바꿈) |
| **Mobile_RPAsset** | HDR 끔 · MSAA 4x · 그림자 10m/1단 · Depth/Opaque 텍스처 끔 · SRP Batcher |
| **카메라** | Far 30m · Post Processing 끔 · 후처리 AA 끔 |
| **Static Batching** | 안 움직이는 렌더러 전부 (잡는 물건 · Rigidbody · UI · 손님 자리 제외) |
| **OpenXR (Android)** | Render Mode → Single Pass Instanced, Foveated Rendering 기능 켬 |
| **Day5_Perf** (씬) | FpsCounter + QuestFoveation |
| **폰트** | 기본 폰트(LiberationSans) 쓰는 텍스트 → NotoSansKR SDF (직접 바꾼 폰트는 그대로) |
| **디버그 텍스트** | ContainerDebugText 끔 (다시 보려면 컴포넌트 체크) |
| **효과음** | GameFlow 이벤트 → Sfx `PlayOneShot` 자동 연결 (파일 있을 때) |
| **오디오 압축** | Android: 15초↑ BGM = Streaming·Vorbis / 짧은 효과음 = ADPCM·Mono |
| 머티리얼 | Synty 머티리얼이 Standard(분홍)면 URP/Lit으로 자동 변환 |

## 2. 방 꾸미기 — 레스토랑 홀 (쿠킹 클래시 VR 구조)

```
          [ 문 밖: 손님 · 고블린 등장 ]
 ┌───────────┐ 문 ┌───────────┐   ← 정면 벽 (가운데 = 문, 플레이어 정면)
 │ 상자     러그 · 게시판     술통 │
 │ 테이블        샹들리에      테이블 │
 창                                창  ← 좌우 벽 (창문 번갈아) + 벽 등
 │ 테이블   (손님 동선 비움)   테이블 │
 │              샹들리에             │
 │          러그 · 손님 자리         │
 │ ═══════════ 카운터 ═══════════ │  ← 촛대 2 + CounterLight
 │ 가마솥        플레이어        술통 │
 └──── 술장 · 연금술 장 · 선반 ────┘  ← 뒤 벽 (플레이어 1.8m 뒤)
```

| 생기는 것 | 내용 |
|---|---|
| 방 방향 | 플레이어 → 손님 자리 방향을 가장 가까운 월드 축에 맞춤 |
| 크기 | 폭 최소 11m (벽 칸 **홀수** → 가운데 칸이 플레이어 정면) · 앞쪽 최소 9m, 월드 UI · 메뉴판이 다 들어가게 |
| 바닥 | `SM_Bld_Castle_Floor_Wood_01` + **문 밖 발판** · 원래 Plane은 렌더러만 끔 (충돌 유지) |
| 정면 벽 | `SM_Bld_Castle_Wall_01`, 가운데 칸 = `SM_Bld_Castle_Wall_Door_Big_01` (크기 안 맞으면 `_Door_01`, 그것도 안 맞으면 빈 칸) |
| 좌우 벽 | 손님 쪽 칸은 창문 벽(`SM_Bld_Castle_Wall_Window_01`)과 번갈아 · 벽 등 4개 (불빛 없는 소품) |
| 뒤 벽 | 연금술 장 · 술장 · 선반, 양옆에 가마솥 · 술통 |
| 홀 | `SM_Prop_Table_Preset_01~04` (음식 차려진 테이블) + 의자 2개씩 — **문 → 손님 자리 직선 1.4m 안과 UI 근처는 비움** |
| 정면 소품 | 문 옆 게시판 · 구석 술통/상자 · 문 앞 러그 · 카운터 앞 러그 |
| 조명 | CounterLight 1 + 샹들리에 2개 아래 HallLight 2 — 그림자 없음, RaidEvent 목록에 추가 (습격 때 붉게) |
| **입구 / 출구 / 고블린 스폰** | **정면 문 밖 0.8m로 이동** → 손님·고블린이 어둠 속에서 문으로 걸어 들어옴 |
| 카메라 배경 | 어두운 갈색 (천장 없음 → 위는 어두운 천장, 문·창 밖은 밤) |

- 원래 입구 위치는 `Day5_Environment/OriginalPoints`에 저장 → **되돌리기** 메뉴가 복구 (메뉴를 여러 번 실행해도 처음 위치 유지)
- 바닥·벽·소품은 Static, 그림자 끔
- **손으로 위치 고친 뒤엔 메뉴 재실행 X** (지우고 새로 만듦)
- 소품이 벽을 보고 있으면 Y 회전 180
- 테이블이 많아 무거우면 `Hall` 안에서 몇 개 삭제 (FPS 우선)
- 문 → 카운터 거리가 길어져 손님 등장이 느려짐 → 답답하면 Customer 프리팹 `moveSpeed` 1.2 → 1.6
- 홀 크기는 스크립트 위쪽 상수 `MinHalfWidth`(5.5) · `MinFrontDepth`(9)로 조절

## 3. 프레임 체크 (72fps 목표)

1. Quest 빌드 → 실행하면 **시야 왼쪽 아래** 숫자
   - 초록 70↑ / 노랑 60대 / 빨강 60 미만
2. 10초마다 Logcat: `[Potender FPS] 평균 71.8 / 최저 64.0 (목표 72)`
   - `adb logcat -s Unity | findstr "Potender FPS"`
3. 꼭 볼 순간: **영업 시작 직후 / 손님 2명 + 쉐이커 흔들기 / 습격 + 폭발**

| FPS 떨어질 때 | 해볼 것 (위에서부터) |
|---|---|
| 방 꾸미기 후 떨어짐 | 테이블 줄이기 → 벽 등·소품 줄이기 → **되돌리기** 메뉴 |
| 습격 때만 떨어짐 | RaidAlarmLight `range` 6 → 4, 폭발 파티클 Max Particles 줄이기 |
| 계속 60대 | QuestFoveation `level` 1 확인 · HallLight 2 → 1 · Mobile_RPAsset Render Scale 0.9 |
| 손님 많을 때 | GameFlow `spawnInterval` 늘리기 (동시 손님 수 ↓) |
| 첫 몇 초만 끊김 | 셰이더 컴파일 → 정상 (녹화는 10초 뒤부터) |

- 더 자세히: Meta Quest Developer Hub ▸ **OVR Metrics Tool** (GPU/CPU %)
- 에디터 FPS는 의미 없음 — **반드시 Quest에서** 확인

## 4. 조명 손보기

| 원하는 느낌 | 바꿀 곳 |
|---|---|
| 더 어둡고 아늑하게 | Directional Light 0.7 → 0.4, 주변광 더 어둡게 (Window ▸ Rendering ▸ Lighting ▸ Environment ▸ Ambient Color) |
| 카운터에 시선 집중 | CounterLight intensity 1.5 → 2.2, HallLight 1.0 |
| 홀이 너무 어두움 | HallLight range 7 → 9 · 주변광 밝게 |
| 습격이 더 극적으로 | RaidEvent `raidIntensity` 0.9 → 0.6, `pulseAmount` 0.5 |
| 그림자가 꼭 필요 | Directional Light Shadows = Hard **하나만** (FPS 확인 필수) |

- 조명 베이크(Generate Lighting)는 **하지 않음** — 시간 오래 걸리고 습격 조명 변화와 충돌

## 5. UI 정리 체크리스트

- [ ] 모든 텍스트 NotoSansKR (메뉴 실행으로 자동)
- [ ] 디버그 텍스트 안 보임 (ContainerDebugText 꺼짐)
- [ ] 타이틀/정산 패널: 플레이어 앞 **1.5~2m**, 눈높이(1.5m) 근처
- [ ] HUD: 메뉴판 반대쪽 (Day4 Fix 완료) · 글자 크기 1.5m에서 읽힘
- [ ] 손님 말풍선: 머리 위 (Synty 모델이 커서 가리면 Customer 프리팹 ▸ UI 높이 +0.2)
- [ ] 별점 ★ 글자 깨짐 없음 (깨지면 NotoSansKR SDF ▸ Atlas Population Mode = Dynamic)
- [ ] 버튼에 손/레이 닿으면 반응 (Hover 색)

## 6. BGM · 효과음 (파일명 키워드로 자동 연결)

| 소리 | 연결 위치 | 파일명 키워드 |
|---|---|---|
| 평소 BGM (루프) | RaidEvent ▸ Bgm_Normal | `bgm` · `tavern` · `music` |
| 습격 BGM (루프) | RaidEvent ▸ Bgm_Raid | `raid` · `battle` · `combat` |
| 영업 시작 | GameFlow ▸ onOpen | `bell` · `door` · `open` · `종` |
| 서빙 성공 | onServeSuccess | `coin` · `cash` · `money` · `ding` · `동전` |
| 잘못 서빙 | onServeWrong | `wrong` · `buzz` · `error` · `땡` |
| 손님 화남 | onCustomerAngry | `angry` · `grumble` · `sigh` |
| 영업 종료 | onClose | `fanfare` · `result` · `jingle` · `마감` |
| 붓기 · 흔들기 · 펑 · 경보 | Day4 메뉴가 연결 | Day4 가이드 6번 |

1. 파일을 `01. Potender/Audio/` 에 넣기
2. **Day4 강도 세팅** (BGM·붓기·펑) + **Day5 폴리싱 세팅** (효과음) 둘 다 다시 실행
3. 효과음은 8초 미만만 자동 연결 (BGM이 효과음으로 잘못 들어가는 것 방지)

- BGM 볼륨 0.3~0.4 (효과음이 묻히지 않게)
- 무료: Pixabay Music/Sound Effects, freesound.org (라이선스 확인 — 발표 자료에 출처 표기)

## 7. 자주 막히는 곳

| 증상 | 원인 / 해결 |
|---|---|
| 캐릭터가 분홍색 | 머티리얼 셰이더 문제 → 메뉴 재실행 (자동 변환) / 안 되면 Window ▸ Rendering ▸ Render Pipeline Converter ▸ Material Upgrade |
| 손님이 공중에 뜨거나 땅에 묻힘 | Customer 프리팹 ModelSwap `hideRenderers`에 몸통이 빠짐 → Body 드래그 |
| 캐릭터가 T자로 팔 벌림 | Synty 기본 포즈 (애니메이션 없음) — 정상. 시간 남으면 Mixamo Idle/Walk → Customer `animator` · `walkBool` |
| 고블린이 초록이 아님 | Synty 셰이더가 `_BaseColor`를 안 씀 → Goblin `colorProperty`를 `_Color` 로 |
| 고블린 맞혀도 안 맞음 | 모델 콜라이더가 아니라 원래 캡슐 트리거로 판정 — 캡슐 크기 확인 (Goblin 프리팹 CapsuleCollider) |
| 손님 말풍선이 얼굴에 묻힘 | Customer 프리팹 ▸ UI 오브젝트 Y 올리기 |
| 손님이 테이블·벽을 통과 | 손님은 직선 이동 → 그 테이블 삭제 / 문이 빈 칸 대신 막혀 있으면 `FrontDoor` 확인 |
| 문 위치가 플레이어 정면이 아님 | 방 방향은 플레이어 → 손님 자리 기준. 손님 자리가 옆으로 많이 치우쳐 있으면 Day5_Environment 전체를 옆으로 이동 |
| 던진 잔이 바닥에서 안 터짐 | 바닥 타일 콜라이더는 꺼 둠 → 원래 Plane 콜라이더 확인 |
| 하늘이 보임 / 너무 어두움 | 카메라 Background 색 · Clear Flags 확인 |
| FPS 표시가 안 보임 | Day5_Perf ▸ FpsCounter `show` / Main Camera 태그 확인 |
| `GetSettingsForBuildTargetGroup` 관련 경고 | OpenXR 패키지 버전 차이 → Project Settings ▸ XR Plug-in Management ▸ OpenXR(Android 탭) ▸ Render Mode = Single Pass Instanced, Foveated Rendering 직접 체크 |

## 8. 컷 기준

1. **방 꾸미기**가 손님 동선/UI를 망치면 → **되돌리기** 메뉴 (입구 위치까지 원래대로)
2. 캐릭터 교체 후 문제 → Customer/Goblin 프리팹의 ModelSwap 컴포넌트 **체크 해제** (원래 도형으로 복귀)
3. FPS 60 밑이면 → 테이블 절반 삭제 → HallLight 1개 → 방 꾸미기 되돌리기
4. BGM/효과음은 **평소 BGM + 동전 + 펑** 3개만 있어도 OK
5. 마지막 빌드 전: FpsCounter `show` 끄기, `Tester` 오브젝트는 그대로 둬도 됨 (키보드 없으면 동작 안 함)
