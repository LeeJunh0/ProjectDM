# 성장트리 노출·선행 조건 전수 조사

조사일: 2026-10-27. **이 문서는 수정 전 설정의 조사 기록이다.** 이후 화면 연결에 맞춰 구매 조건과 노출 규칙을 조정했다. 조사 당시 게임이 실제로 불러오는 `GrowthTrees.asset`의 활성 노드 159개를 조사했으며, 게임 저장 데이터는 사용하거나 수정하지 않았다.

## 해석 기준

- **구매 기준 부모**는 구매 조건(`parent`, `parentRanks`)에 쓰인다. 앞 단계 구매(`startRank`)나 별도 효과 조건(`requires`)이 추가로 있을 수 있다.
- **화면 연결 부모**는 화면에서 실제 연결선을 그리는 `displayParent`이며, 추가 표시 연결(`displayLinks`)도 별도로 고려했다.
- 두 부모가 다른 것만으로 결함이라고 단정할 수 없다. 다른 경로로 접근 가능할 수도 있어, 아래 진행 시뮬레이션 결과와 구분했다.
- 초기 노출은 **새 저장·몬스터 단계 0**의 현재 코드 기준이다. 루트에 직접 연결되지 않은 노출은 기존 구매가 없는 상황에서 표시 경로상 루트 한 칸 너머에 있는 노드다.
- 진행 시뮬레이션은 별개의 가상 새 저장에서 몬스터 단계 4, 각 구매마다 충분한 재화를 가정했다. 루트에 직접 연결된 노드와 **구매한 노드에 화면 연결로 한 칸 인접한 노드**만 보이도록 가정하고, 표시된 노드 중 구매 가능한 모든 단계를 더 이상 살 수 없을 때까지 구매했다. 부모·자식 양방향과 추가 표시 연결을 모두 고려했다.
- **직접 막힘**은 그 시점에 보이지만 구매 조건이 충족되지 않은 노드다. **뒤에서 숨겨짐**은 같은 시점에 아직 보이지 않는 노드로, 각각이 독립적인 오류라는 뜻은 아니다. 실제 사용자 저장 상태와는 결과가 다를 수 있다.

## 트리별 결과

| 트리 | 노드 | 구매 부모와 표시 부모가 다름 | 현재 초기 노출 | 초기 노출 중 루트에 직접 연결되지 않음 | 시뮬레이션 미완료 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 공통 성장 | 92 | 72 | 29 | 25 | 87 |
| 마법미사일 | 16 | 13 | 7 | 5 | 0 |
| 번개 | 16 | 13 | 7 | 5 | 0 |
| 화염 | 16 | 13 | 7 | 5 | 0 |
| 빙결 | 19 | 16 | 10 | 8 | 0 |

공통 트리는 15번 구매한 뒤 92개 중 87개가 미완료로 남았다. 이 87개는 직접 막힌 7개와 이후 숨겨진 80개다. 캐릭터 전용 네 트리는 같은 가정에서 모두 완료할 수 있었다.

## 공통 트리에서 직접 막힌 7개

| 노드 | ID | 시뮬레이션에서 먼저 충족되지 않은 조건 | 화면에서 앞에 연결된 노드 |
| --- | --- | --- | --- |
| 공격력 II | `damage_2` | 공격속도 I 0/1단계 필요 | 공격력 I (`damage_1`) |
| 경험치 획득량 II | `xp_2` | 필드 개체 수 I 0/1단계 필요 | 경험치 획득량 I (`xp_1`) |
| 최대 마나 II | `time_2` | 처치 마나 흡수 I 0/1단계 필요 | 최대 마나 I (`time_1`) |
| 재화 획득량 III | `yield_3` | 처치 마나 흡수 II 0/3단계 필요 | 재화 획득량 II (`yield_2`) |
| 공격력 VIII | `damage_8` | 같은 성장의 앞 단계 공격력 VII (`damage_7`) 완료 필요 | 공격력 VII (`damage_7`) |
| 최대 마나 IV | `time_4` | 같은 성장의 앞 단계 최대 마나 III (`time_3`) 완료 필요 | 최대 마나 III (`time_3`) |
| 재화 획득량 V | `yield_5` | 같은 성장의 앞 단계 재화 획득량 IV (`yield_4`) 완료 필요 | 재화 획득량 IV (`yield_4`) |

## 그 시점에 뒤에서 숨겨진 공통 노드 80개

직접 막힌 7개에서 이어지는 결과이며, 이 80개를 각각 별도의 원인으로 계산하면 안 된다. 이름이 같은 노드는 ID로 구분한다.

- **공격속도** (`speed`): 공격속도 I (`speed_1`), 공격속도 II (`speed_2`), 공격속도 III (`speed_3`), 공격속도 IV (`speed_4`), 공격속도 V (`speed_5`), 공격속도 VI (`speed_6`), 공격속도 VII (`speed_7`), 공격속도 VIII (`speed_8`), 공격속도 IX (`speed_9`), 공격속도 X (`speed_10`)
- **필드 개체 수** (`population`): 필드 개체 수 I (`population_1`), 필드 개체 수 II (`population_2`), 필드 개체 수 III (`population_3`), 필드 개체 수 IV (`population_4`), 몬스터 생성량 (`population_5`), 몬스터 생성량 (`population_6`), 몬스터 생성량 (`population_7`), 몬스터 생성량 (`population_8`), 몬스터 생성량 (`population_9`), 몬스터 생성량 (`population_10`), 몬스터 생성량 (`population_11`)
- **처치 마나 흡수** (`extension`): 처치 마나 흡수 I (`extension_1`), 처치 마나 흡수 II (`extension_2`), 처치 마나 흡수 III (`extension_3`), 처치 마나 흡수 IV (`extension_4`)
- **리스폰 속도** (`respawn`): 리스폰 속도 I (`respawn_1`), 리스폰 속도 II (`respawn_2`), 리스폰 속도 III (`respawn_3`), 리스폰 속도 IV (`respawn_4`), 리스폰 속도 V (`respawn_5`)
- **공격력** (`damage`): 공격력 III (`damage_3`), 공격력 IV (`damage_4`), 공격력 V (`damage_5`), 공격력 VI (`damage_6`), 공격력 VII (`damage_7`), 공격력 IX (`damage_9`), 공격력 X (`damage_10`), 공격력 XI (`damage_11`), 공격력 XII (`damage_12`), 공격력 XIII (`damage_13`), 공격력 XIV (`damage_14`)
- **경험치 획득량** (`xp`): 경험치 획득량 III (`xp_3`), 경험치 획득량 IV (`xp_4`), 경험치 획득량 V (`xp_5`)
- **최대 마나** (`time`): 최대 마나 III (`time_3`)
- **재화 획득량** (`yield`): 재화 획득량 IV (`yield_4`)
- **은화 확률** (`silverDropChance`): 은화 확률 I (`silverDropChance_1`), 은화 확률 II (`silverDropChance_2`), 은화 확률 III (`silverDropChance_3`)
- **상위 경험치 확률** (`experienceGradeChance`): 상위 경험치 확률 I (`experienceGradeChance_1`), 상위 경험치 확률 II (`experienceGradeChance_2`), 상위 경험치 확률 III (`experienceGradeChance_3`)
- **금화 확률** (`goldDropChance`): 금화 확률 I (`goldDropChance_1`), 금화 확률 II (`goldDropChance_2`), 금화 확률 III (`goldDropChance_3`)
- **에메랄드 화폐 확률** (`emeraldDropChance`): 에메랄드 화폐 확률 I (`emeraldDropChance_1`), 에메랄드 화폐 확률 II (`emeraldDropChance_2`), 에메랄드 화폐 확률 III (`emeraldDropChance_3`)
- **자수정 화폐 확률** (`amethystDropChance`): 자수정 화폐 확률 I (`amethystDropChance_1`), 자수정 화폐 확률 II (`amethystDropChance_2`), 자수정 화폐 확률 III (`amethystDropChance_3`)
- **최대 마나** (`health`): 최대 마나 (`health_1`)
- **이동속도** (`movement`): 이동속도 (`movement_1`)
- **치명타 확률** (`criticalChance`): 치명타 확률 (`criticalChance_1`)
- **치명타 피해량** (`criticalDamage`): 치명타 피해량 (`criticalDamage_1`)
- **피격 후 무적 시간** (`invulnerability`): 피격 후 무적 시간 (`invulnerability_1`)
- **아이템 끌어오기 범위** (`magnet`): 아이템 끌어오기 범위 (`magnet_1`)
- **행운** (`luck`): 행운 (`luck_1`)
- **몬스터 최대 개체 수** (`populationCap`): 몬스터 최대 개체 수 (`populationCap_1`), 몬스터 최대 개체 수 (`populationCap_2`), 몬스터 최대 개체 수 (`populationCap_3`), 몬스터 최대 개체 수 (`populationCap_4`), 몬스터 최대 개체 수 (`populationCap_5`), 몬스터 최대 개체 수 (`populationCap_6`), 몬스터 최대 개체 수 (`populationCap_7`)
- **한 번에 생성되는 몬스터** (`spawnBatch`): 한 번에 생성되는 몬스터 (`spawnBatch_1`), 한 번에 생성되는 몬스터 (`spawnBatch_2`), 한 번에 생성되는 몬스터 (`spawnBatch_3`), 한 번에 생성되는 몬스터 (`spawnBatch_4`)
- **자석 드랍 확률** (`magnetDropChance`): 자석 드랍 확률 (`magnetDropChance_1`)

## 새 저장에서 루트 한 칸 너머인데 표시되는 노드 전체

현재 코드가 해금 가능 여부와 구매 기준 부모를 사용하기 때문에 나타나는 목록이다. 표시 부모와 다른 추가 연결이 있더라도 새 저장에서는 루트 외에 구매한 노드가 없다.

### 공통 성장 (25개)

- 공격속도 I (`speed_1`): 표시 부모 `damage_4`, 구매 부모 `ROOT`
- 필드 개체 수 I (`population_1`): 표시 부모 `experienceGradeChance_3`, 구매 부모 `ROOT`
- 처치 마나 흡수 I (`extension_1`): 표시 부모 `time_2`, 구매 부모 `ROOT`
- 공격력 II (`damage_2`): 표시 부모 `damage_1`, 구매 부모 `speed_1`
- 공격속도 II (`speed_2`): 표시 부모 `speed_1`, 구매 부모 `damage_1`
- 리스폰 속도 I (`respawn_1`): 표시 부모 `magnet_1`, 구매 부모 `population_1`
- 필드 개체 수 II (`population_2`): 표시 부모 `population_1`, 구매 부모 `yield_1`
- 처치 마나 흡수 II (`extension_2`): 표시 부모 `extension_1`, 구매 부모 `xp_1`
- 경험치 획득량 II (`xp_2`): 표시 부모 `xp_1`, 구매 부모 `population_1`
- 공격력 III (`damage_3`): 표시 부모 `damage_2`, 구매 부모 `time_1`
- 공격속도 III (`speed_3`): 표시 부모 `speed_2`, 구매 부모 `extension_1`
- 최대 마나 II (`time_2`): 표시 부모 `time_1`, 구매 부모 `extension_1`
- 재화 획득량 II (`yield_2`): 표시 부모 `yield_1`, 구매 부모 `time_1`
- 공격력 IV (`damage_4`): 표시 부모 `damage_3`, 구매 부모 `damage_1`
- 처치 마나 흡수 III (`extension_3`): 표시 부모 `extension_2`, 구매 부모 `damage_1`
- 리스폰 속도 III (`respawn_3`): 표시 부모 `respawn_2`, 구매 부모 `ROOT`
- 은화 확률 I (`silverDropChance_1`): 표시 부모 `yield_4`, 구매 부모 `yield_1`
- 상위 경험치 확률 I (`experienceGradeChance_1`): 표시 부모 `xp_4`, 구매 부모 `xp_1`
- 최대 마나 (`health_1`): 표시 부모 `time_3`, 구매 부모 `population_1`
- 이동속도 (`movement_1`): 표시 부모 `experienceGradeChance_2`, 구매 부모 `time_1`
- 치명타 확률 (`criticalChance_1`): 표시 부모 `damage_13`, 구매 부모 `speed_1`
- 치명타 피해량 (`criticalDamage_1`): 표시 부모 `speed_4`, 구매 부모 `ROOT`
- 행운 (`luck_1`): 표시 부모 `magnet_1`, 구매 부모 `xp_1`
- 몬스터 최대 개체 수 (`populationCap_1`): 표시 부모 `movement_1`, 구매 부모 `population_1`
- 한 번에 생성되는 몬스터 (`spawnBatch_1`): 표시 부모 `movement_1`, 구매 부모 `population_1`

### 마법미사일 (5개)

- 빠른 시전 (`magic.projectile.speed`): 표시 부모 `magic.projectile.damage`, 구매 부모 `ROOT`
- 추가 마력탄 (`magic.projectile.count`): 표시 부모 `magic.projectile.speed`, 구매 부모 `magic.projectile.damage`
- 관통 마력탄 (`magic.projectile.pierce`): 표시 부모 `magic.projectile.count`, 구매 부모 `magic.projectile.damage`
- 유도 마력탄 (`magic.projectile.homing`): 표시 부모 `magic.projectile.pierce`, 구매 부모 `magic.projectile.damage`
- 분열 마력탄 (`magic.projectile.split`): 표시 부모 `magic.projectile.homing`, 구매 부모 `magic.projectile.damage`

### 번개 (5개)

- 번개 시전 속도 (`magic.lightning.speed`): 표시 부모 `magic.lightning.damage`, 구매 부모 `ROOT`
- 추가 연쇄 (`magic.lightning.jumps`): 표시 부모 `magic.lightning.speed`, 구매 부모 `magic.lightning.damage`
- 연쇄 도약 거리 (`magic.lightning.range`): 표시 부모 `magic.lightning.jumps`, 구매 부모 `magic.lightning.damage`
- 번개 재타격 (`magic.lightning.echo`): 표시 부모 `magic.lightning.range`, 구매 부모 `magic.lightning.damage`
- 분기 번개 (`magic.lightning.fork`): 표시 부모 `magic.lightning.echo`, 구매 부모 `magic.lightning.damage`

### 화염 (5개)

- 화염 시전 속도 (`magic.flame.speed`): 표시 부모 `magic.flame.damage`, 구매 부모 `ROOT`
- 화염 사거리 (`magic.flame.range`): 표시 부모 `magic.flame.speed`, 구매 부모 `magic.flame.damage`
- 넓은 화염 (`magic.flame.width`): 표시 부모 `magic.flame.range`, 구매 부모 `magic.flame.damage`
- 지속 화상 (`magic.flame.burn`): 표시 부모 `magic.flame.width`, 구매 부모 `magic.flame.damage`
- 화상 전파 (`magic.flame.spread`): 표시 부모 `magic.flame.burn`, 구매 부모 `magic.flame.damage`

### 빙결 (8개)

- 빠른 재시전 (`magic.frost.speed`): 표시 부모 `magic.frost.damage`, 구매 부모 `ROOT`
- 추가 얼음 조각 (`magic.frost.count`): 표시 부모 `magic.frost.speed`, 구매 부모 `magic.frost.damage`
- 빠른 공전 (`magic.frost.orbitSpeed`): 표시 부모 `magic.frost.count`, 구매 부모 `magic.frost.damage`
- 넓은 궤도 (`magic.frost.range`): 표시 부모 `magic.frost.orbitSpeed`, 구매 부모 `magic.frost.damage`
- 큰 얼음 조각 (`magic.frost.size`): 표시 부모 `magic.frost.range`, 구매 부모 `magic.frost.damage`
- 짙은 냉기 (`magic.frost.chill`): 표시 부모 `magic.frost.size`, 구매 부모 `magic.frost.damage`
- 오래가는 빙결 (`magic.frost.freezeDuration`): 표시 부모 `magic.frost.chill`, 구매 부모 `magic.frost.damage`
- 이중 얼음 궤도 (`magic.frost.rings`): 표시 부모 `magic.frost.freezeDuration`, 구매 부모 `magic.frost.damage`

## 구매 기준 부모와 화면 연결 부모가 다른 노드 전체

총 127개. **이 표는 차이의 전수 목록이며, 전부 진행 불가라고 판정한 목록은 아니다.** `parentRanks`가 0이면 구매 부모의 단계 요구는 없다. 별도 효과 선행 조건과 같은 성장의 앞 단계 조건은 구매 가능 여부에 추가로 적용된다.

### 공통 성장 (72개)

| 노드 | 구매 기준 부모 · 필요 단계 | 화면 연결 부모 | 추가 화면 연결 |
| --- | --- | --- | --- |
| 공격속도 I (`speed_1`) | 루트 · 부모 단계 조건 없음 | 공격력 IV (`damage_4`) | — |
| 필드 개체 수 I (`population_1`) | 루트 · 부모 단계 조건 없음 | 상위 경험치 확률 III (`experienceGradeChance_3`) | — |
| 처치 마나 흡수 I (`extension_1`) | 루트 · 부모 단계 조건 없음 | 최대 마나 II (`time_2`) | — |
| 공격력 II (`damage_2`) | 공격속도 I (`speed_1`) · 1단계 | 공격력 I (`damage_1`) | — |
| 공격속도 II (`speed_2`) | 공격력 I (`damage_1`) · 1단계 | 공격속도 I (`speed_1`) | — |
| 리스폰 속도 I (`respawn_1`) | 필드 개체 수 I (`population_1`) · 1단계 | 아이템 끌어오기 범위 (`magnet_1`) | — |
| 필드 개체 수 II (`population_2`) | 재화 획득량 I (`yield_1`) · 1단계 | 필드 개체 수 I (`population_1`) | — |
| 처치 마나 흡수 II (`extension_2`) | 경험치 획득량 I (`xp_1`) · 1단계 | 처치 마나 흡수 I (`extension_1`) | — |
| 경험치 획득량 II (`xp_2`) | 필드 개체 수 I (`population_1`) · 1단계 | 경험치 획득량 I (`xp_1`) | — |
| 공격력 III (`damage_3`) | 최대 마나 I (`time_1`) · 1단계 | 공격력 II (`damage_2`) | — |
| 공격속도 III (`speed_3`) | 처치 마나 흡수 I (`extension_1`) · 1단계 | 공격속도 II (`speed_2`) | — |
| 최대 마나 II (`time_2`) | 처치 마나 흡수 I (`extension_1`) · 1단계 | 최대 마나 I (`time_1`) | — |
| 재화 획득량 II (`yield_2`) | 최대 마나 I (`time_1`) · 1단계 | 재화 획득량 I (`yield_1`) | — |
| 공격력 IV (`damage_4`) | 공격력 I (`damage_1`) · 1단계 | 공격력 III (`damage_3`) | — |
| 공격속도 IV (`speed_4`) | 공격력 II (`damage_2`) · 3단계 | 공격속도 III (`speed_3`) | — |
| 리스폰 속도 II (`respawn_2`) | 공격속도 II (`speed_2`) · 3단계 | 리스폰 속도 I (`respawn_1`) | — |
| 경험치 획득량 III (`xp_3`) | 공격력 III (`damage_3`) · 3단계 | 경험치 획득량 II (`xp_2`) | — |
| 공격력 V (`damage_5`) | 재화 획득량 II (`yield_2`) · 3단계 | 공격력 IV (`damage_4`) | — |
| 공격속도 V (`speed_5`) | 재화 획득량 II (`yield_2`) · 3단계 | 공격속도 IV (`speed_4`) | — |
| 최대 마나 III (`time_3`) | 필드 개체 수 II (`population_2`) · 3단계 | 최대 마나 II (`time_2`) | — |
| 재화 획득량 III (`yield_3`) | 처치 마나 흡수 II (`extension_2`) · 3단계 | 재화 획득량 II (`yield_2`) | — |
| 필드 개체 수 III (`population_3`) | 경험치 획득량 II (`xp_2`) · 3단계 | 필드 개체 수 II (`population_2`) | — |
| 처치 마나 흡수 III (`extension_3`) | 공격력 I (`damage_1`) · 1단계 | 처치 마나 흡수 II (`extension_2`) | — |
| 공격력 VI (`damage_6`) | 최대 마나 II (`time_2`) · 3단계 | 공격력 V (`damage_5`) | 공격력 III (`damage_3`) |
| 공격속도 VI (`speed_6`) | 최대 마나 II (`time_2`) · 3단계 | 공격속도 V (`speed_5`) | 공격속도 III (`speed_3`) |
| 리스폰 속도 III (`respawn_3`) | 루트 · 부모 단계 조건 없음 | 리스폰 속도 II (`respawn_2`) | — |
| 공격력 VII (`damage_7`) | 공격속도 IV (`speed_4`) · 3단계 | 공격력 VI (`damage_6`) | 공격력 II (`damage_2`) |
| 공격속도 VII (`speed_7`) | 리스폰 속도 II (`respawn_2`) · 3단계 | 공격속도 VI (`speed_6`) | 공격속도 II (`speed_2`) |
| 경험치 획득량 IV (`xp_4`) | 리스폰 속도 II (`respawn_2`) · 3단계 | 경험치 획득량 III (`xp_3`) | — |
| 공격력 VIII (`damage_8`) | 경험치 획득량 III (`xp_3`) · 3단계 | 공격력 VII (`damage_7`) | 공격력 I (`damage_1`) |
| 공격속도 VIII (`speed_8`) | 공격력 V (`damage_5`) · 3단계 | 공격속도 VII (`speed_7`) | 공격속도 I (`speed_1`) |
| 재화 획득량 IV (`yield_4`) | 최대 마나 III (`time_3`) · 3단계 | 재화 획득량 III (`yield_3`) | — |
| 리스폰 속도 IV (`respawn_4`) | 재화 획득량 III (`yield_3`) · 3단계 | 리스폰 속도 III (`respawn_3`) | — |
| 처치 마나 흡수 IV (`extension_4`) | 필드 개체 수 III (`population_3`) · 3단계 | 처치 마나 흡수 III (`extension_3`) | 처치 마나 흡수 I (`extension_1`) |
| 필드 개체 수 IV (`population_4`) | 리스폰 속도 I (`respawn_1`) · 3단계 | 필드 개체 수 III (`population_3`) | — |
| 최대 마나 IV (`time_4`) | 리스폰 속도 I (`respawn_1`) · 1단계 | 최대 마나 III (`time_3`) | 최대 마나 I (`time_1`) |
| 공격력 IX (`damage_9`) | 공격속도 VI (`speed_6`) · 3단계 | 공격력 VIII (`damage_8`) | — |
| 공격속도 IX (`speed_9`) | 리스폰 속도 III (`respawn_3`) · 3단계 | 공격속도 VIII (`speed_8`) | — |
| 공격력 X (`damage_10`) | 공격력 IV (`damage_4`) · 1단계 | 공격력 IX (`damage_9`) | 공격력 VII (`damage_7`) |
| 공격속도 X (`speed_10`) | 공격력 VII (`damage_7`) · 3단계 | 공격속도 IX (`speed_9`) | 공격속도 VII (`speed_7`) |
| 경험치 획득량 V (`xp_5`) | 공격속도 VII (`speed_7`) · 3단계 | 경험치 획득량 IV (`xp_4`) | 경험치 획득량 II (`xp_2`) |
| 공격력 XI (`damage_11`) | 공격속도 VIII (`speed_8`) · 3단계 | 공격력 X (`damage_10`) | 공격력 VI (`damage_6`) |
| 재화 획득량 V (`yield_5`) | 공격속도 VIII (`speed_8`) · 3단계 | 재화 획득량 IV (`yield_4`) | 재화 획득량 II (`yield_2`) |
| 리스폰 속도 V (`respawn_5`) | 재화 획득량 IV (`yield_4`) · 3단계 | 리스폰 속도 IV (`respawn_4`) | 리스폰 속도 II (`respawn_2`) |
| 공격력 XII (`damage_12`) | 필드 개체 수 IV (`population_4`) · 1단계 | 공격력 XI (`damage_11`) | 공격력 V (`damage_5`) |
| 공격력 XIII (`damage_13`) | 공격력 VI (`damage_6`) · 3단계 | 공격력 XII (`damage_12`) | — |
| 공격력 XIV (`damage_14`) | 공격속도 IX (`speed_9`) · 3단계 | 공격력 XIII (`damage_13`) | 공격력 XI (`damage_11`) |
| 은화 확률 I (`silverDropChance_1`) | 재화 획득량 I (`yield_1`) · 1단계 | 재화 획득량 IV (`yield_4`) | — |
| 상위 경험치 확률 I (`experienceGradeChance_1`) | 경험치 획득량 I (`xp_1`) · 1단계 | 경험치 획득량 IV (`xp_4`) | — |
| 은화 확률 II (`silverDropChance_2`) | 재화 획득량 III (`yield_3`) · 2단계 | 은화 확률 I (`silverDropChance_1`) | — |
| 상위 경험치 확률 II (`experienceGradeChance_2`) | 경험치 획득량 III (`xp_3`) · 2단계 | 상위 경험치 확률 I (`experienceGradeChance_1`) | — |
| 은화 확률 III (`silverDropChance_3`) | 재화 획득량 V (`yield_5`) · 3단계 | 은화 확률 II (`silverDropChance_2`) | — |
| 상위 경험치 확률 III (`experienceGradeChance_3`) | 경험치 획득량 V (`xp_5`) · 3단계 | 상위 경험치 확률 II (`experienceGradeChance_2`) | — |
| 금화 확률 I (`goldDropChance_1`) | 재화 획득량 II (`yield_2`) · 1단계 | 재화 획득량 IV (`yield_4`) | — |
| 금화 확률 II (`goldDropChance_2`) | 공격력 III (`damage_3`) · 2단계 | 금화 확률 I (`goldDropChance_1`) | — |
| 금화 확률 III (`goldDropChance_3`) | 공격력 IV (`damage_4`) · 3단계 | 금화 확률 II (`goldDropChance_2`) | — |
| 에메랄드 화폐 확률 I (`emeraldDropChance_1`) | 공격속도 III (`speed_3`) · 1단계 | 은화 확률 II (`silverDropChance_2`) | — |
| 에메랄드 화폐 확률 II (`emeraldDropChance_2`) | 재화 획득량 III (`yield_3`) · 2단계 | 에메랄드 화폐 확률 I (`emeraldDropChance_1`) | — |
| 에메랄드 화폐 확률 III (`emeraldDropChance_3`) | 공격력 V (`damage_5`) · 3단계 | 에메랄드 화폐 확률 II (`emeraldDropChance_2`) | — |
| 자수정 화폐 확률 I (`amethystDropChance_1`) | 최대 마나 II (`time_2`) · 1단계 | 은화 확률 III (`silverDropChance_3`) | — |
| 자수정 화폐 확률 II (`amethystDropChance_2`) | 리스폰 속도 III (`respawn_3`) · 2단계 | 자수정 화폐 확률 I (`amethystDropChance_1`) | — |
| 자수정 화폐 확률 III (`amethystDropChance_3`) | 공격속도 V (`speed_5`) · 3단계 | 자수정 화폐 확률 II (`amethystDropChance_2`) | — |
| 최대 마나 (`health_1`) | 필드 개체 수 I (`population_1`) · 1단계 | 최대 마나 III (`time_3`) | — |
| 이동속도 (`movement_1`) | 최대 마나 I (`time_1`) · 1단계 | 상위 경험치 확률 II (`experienceGradeChance_2`) | — |
| 치명타 확률 (`criticalChance_1`) | 공격속도 I (`speed_1`) · 1단계 | 공격력 XIII (`damage_13`) | — |
| 치명타 피해량 (`criticalDamage_1`) | 루트 · 부모 단계 조건 없음 | 공격속도 IV (`speed_4`) | — |
| 피격 후 무적 시간 (`invulnerability_1`) | 리스폰 속도 II (`respawn_2`) · 1단계 | 처치 마나 흡수 II (`extension_2`) | — |
| 아이템 끌어오기 범위 (`magnet_1`) | 최대 마나 III (`time_3`) · 1단계 | 경험치 획득량 IV (`xp_4`) | — |
| 행운 (`luck_1`) | 경험치 획득량 I (`xp_1`) · 1단계 | 아이템 끌어오기 범위 (`magnet_1`) | — |
| 몬스터 최대 개체 수 (`populationCap_1`) | 필드 개체 수 I (`population_1`) · 1단계 | 이동속도 (`movement_1`) | — |
| 한 번에 생성되는 몬스터 (`spawnBatch_1`) | 필드 개체 수 I (`population_1`) · 1단계 | 이동속도 (`movement_1`) | — |
| 자석 드랍 확률 (`magnetDropChance_1`) | 아이템 끌어오기 범위 (`magnet_1`) · 1단계 | 필드 개체 수 IV (`population_4`) | — |

### 마법미사일 (13개)

| 노드 | 구매 기준 부모 · 필요 단계 | 화면 연결 부모 | 추가 화면 연결 |
| --- | --- | --- | --- |
| 빠른 시전 (`magic.projectile.speed`) | 루트 · 부모 단계 조건 없음 | 마력탄 위력 (`magic.projectile.damage`) | — |
| 추가 마력탄 (`magic.projectile.count`) | 마력탄 위력 (`magic.projectile.damage`) · 1단계 | 빠른 시전 (`magic.projectile.speed`) | — |
| 관통 마력탄 (`magic.projectile.pierce`) | 마력탄 위력 (`magic.projectile.damage`) · 1단계 | 추가 마력탄 (`magic.projectile.count`) | — |
| 유도 마력탄 (`magic.projectile.homing`) | 마력탄 위력 (`magic.projectile.damage`) · 1단계 | 관통 마력탄 (`magic.projectile.pierce`) | 빠른 시전 (`magic.projectile.speed`) |
| 마력 광선 해금 (`magic.projectile.beamUnlock`) | 마력탄 위력 (`magic.projectile.damage`) · 1단계 | 루트 | — |
| 마력 광선 굵기 (`magic.projectile.beamWidth`) | 마력 광선 해금 (`magic.projectile.beamUnlock`) · 1단계 | 마력 광선 위력 (`magic.projectile.beamPower`) | — |
| 마력 광선 광선 수 (`magic.projectile.beamCount`) | 마력 광선 해금 (`magic.projectile.beamUnlock`) · 1단계 | 마력 광선 굵기 (`magic.projectile.beamWidth`) | — |
| 마력 광선 회전 속도 (`magic.projectile.beamSweep`) | 마력 광선 해금 (`magic.projectile.beamUnlock`) · 1단계 | 마력 광선 광선 수 (`magic.projectile.beamCount`) | 마력 광선 위력 (`magic.projectile.beamPower`) |
| 마력 위성 해금 (`magic.projectile.satelliteUnlock`) | 마력 광선 해금 (`magic.projectile.beamUnlock`) · 1단계 | 루트 | — |
| 마력 위성 위성 수 (`magic.projectile.satelliteCount`) | 마력 위성 해금 (`magic.projectile.satelliteUnlock`) · 1단계 | 마력 위성 위력 (`magic.projectile.satellitePower`) | — |
| 마력 위성 발사 속도 (`magic.projectile.satelliteRate`) | 마력 위성 해금 (`magic.projectile.satelliteUnlock`) · 1단계 | 마력 위성 위성 수 (`magic.projectile.satelliteCount`) | — |
| 마력 위성 일제 사격 (`magic.projectile.satelliteVolley`) | 마력 위성 해금 (`magic.projectile.satelliteUnlock`) · 1단계 | 마력 위성 발사 속도 (`magic.projectile.satelliteRate`) | 마력 위성 위력 (`magic.projectile.satellitePower`) |
| 분열 마력탄 (`magic.projectile.split`) | 마력탄 위력 (`magic.projectile.damage`) · 1단계 | 유도 마력탄 (`magic.projectile.homing`) | 마력탄 위력 (`magic.projectile.damage`) |

### 번개 (13개)

| 노드 | 구매 기준 부모 · 필요 단계 | 화면 연결 부모 | 추가 화면 연결 |
| --- | --- | --- | --- |
| 번개 시전 속도 (`magic.lightning.speed`) | 루트 · 부모 단계 조건 없음 | 번개 위력 (`magic.lightning.damage`) | — |
| 추가 연쇄 (`magic.lightning.jumps`) | 번개 위력 (`magic.lightning.damage`) · 1단계 | 번개 시전 속도 (`magic.lightning.speed`) | — |
| 연쇄 도약 거리 (`magic.lightning.range`) | 번개 위력 (`magic.lightning.damage`) · 1단계 | 추가 연쇄 (`magic.lightning.jumps`) | — |
| 번개 재타격 (`magic.lightning.echo`) | 번개 위력 (`magic.lightning.damage`) · 1단계 | 연쇄 도약 거리 (`magic.lightning.range`) | 번개 시전 속도 (`magic.lightning.speed`) |
| 전기 구체 해금 (`magic.lightning.orbUnlock`) | 번개 위력 (`magic.lightning.damage`) · 1단계 | 루트 | — |
| 전기 구체 방전 반경 (`magic.lightning.orbRange`) | 전기 구체 해금 (`magic.lightning.orbUnlock`) · 1단계 | 전기 구체 위력 (`magic.lightning.orbPower`) | — |
| 전기 구체 구체 수 (`magic.lightning.orbCount`) | 전기 구체 해금 (`magic.lightning.orbUnlock`) · 1단계 | 전기 구체 방전 반경 (`magic.lightning.orbRange`) | — |
| 전류 연결 위력 (`magic.lightning.orbLink`) | 전기 구체 해금 (`magic.lightning.orbUnlock`) · 1단계 | 전기 구체 구체 수 (`magic.lightning.orbCount`) | 전기 구체 위력 (`magic.lightning.orbPower`) |
| 낙뢰 해금 (`magic.lightning.thunderUnlock`) | 전기 구체 해금 (`magic.lightning.orbUnlock`) · 1단계 | 루트 | — |
| 낙뢰 폭발 반경 (`magic.lightning.thunderRange`) | 낙뢰 해금 (`magic.lightning.thunderUnlock`) · 1단계 | 낙뢰 위력 (`magic.lightning.thunderPower`) | — |
| 낙뢰 낙뢰 수 (`magic.lightning.thunderCount`) | 낙뢰 해금 (`magic.lightning.thunderUnlock`) · 1단계 | 낙뢰 폭발 반경 (`magic.lightning.thunderRange`) | — |
| 낙뢰 잔류 연쇄 (`magic.lightning.thunderChain`) | 낙뢰 해금 (`magic.lightning.thunderUnlock`) · 1단계 | 낙뢰 낙뢰 수 (`magic.lightning.thunderCount`) | 낙뢰 위력 (`magic.lightning.thunderPower`) |
| 분기 번개 (`magic.lightning.fork`) | 번개 위력 (`magic.lightning.damage`) · 1단계 | 번개 재타격 (`magic.lightning.echo`) | 번개 위력 (`magic.lightning.damage`) |

### 화염 (13개)

| 노드 | 구매 기준 부모 · 필요 단계 | 화면 연결 부모 | 추가 화면 연결 |
| --- | --- | --- | --- |
| 화염 시전 속도 (`magic.flame.speed`) | 루트 · 부모 단계 조건 없음 | 화염 위력 (`magic.flame.damage`) | — |
| 화염 사거리 (`magic.flame.range`) | 화염 위력 (`magic.flame.damage`) · 1단계 | 화염 시전 속도 (`magic.flame.speed`) | — |
| 넓은 화염 (`magic.flame.width`) | 화염 위력 (`magic.flame.damage`) · 1단계 | 화염 사거리 (`magic.flame.range`) | — |
| 지속 화상 (`magic.flame.burn`) | 화염 위력 (`magic.flame.damage`) · 1단계 | 넓은 화염 (`magic.flame.width`) | 화염 시전 속도 (`magic.flame.speed`) |
| 불길 지대 해금 (`magic.flame.firefieldUnlock`) | 화염 위력 (`magic.flame.damage`) · 1단계 | 루트 | — |
| 불길 지대 범위 (`magic.flame.firefieldRange`) | 불길 지대 해금 (`magic.flame.firefieldUnlock`) · 1단계 | 불길 지대 위력 (`magic.flame.firefieldPower`) | — |
| 불길 지대 개수 (`magic.flame.firefieldCount`) | 불길 지대 해금 (`magic.flame.firefieldUnlock`) · 1단계 | 불길 지대 범위 (`magic.flame.firefieldRange`) | — |
| 불길 지대 지속시간 (`magic.flame.firefieldDuration`) | 불길 지대 해금 (`magic.flame.firefieldUnlock`) · 1단계 | 불길 지대 개수 (`magic.flame.firefieldCount`) | 불길 지대 위력 (`magic.flame.firefieldPower`) |
| 유성 낙하 해금 (`magic.flame.meteorUnlock`) | 불길 지대 해금 (`magic.flame.firefieldUnlock`) · 1단계 | 루트 | — |
| 유성 낙하 폭발 반경 (`magic.flame.meteorRange`) | 유성 낙하 해금 (`magic.flame.meteorUnlock`) · 1단계 | 유성 낙하 위력 (`magic.flame.meteorPower`) | — |
| 유성 낙하 운석 수 (`magic.flame.meteorCount`) | 유성 낙하 해금 (`magic.flame.meteorUnlock`) · 1단계 | 유성 낙하 폭발 반경 (`magic.flame.meteorRange`) | — |
| 유성 낙하 화상 증폭 (`magic.flame.meteorIgnite`) | 유성 낙하 해금 (`magic.flame.meteorUnlock`) · 1단계 | 유성 낙하 운석 수 (`magic.flame.meteorCount`) | 유성 낙하 위력 (`magic.flame.meteorPower`) |
| 화상 전파 (`magic.flame.spread`) | 화염 위력 (`magic.flame.damage`) · 1단계 | 지속 화상 (`magic.flame.burn`) | 화염 위력 (`magic.flame.damage`) |

### 빙결 (16개)

| 노드 | 구매 기준 부모 · 필요 단계 | 화면 연결 부모 | 추가 화면 연결 |
| --- | --- | --- | --- |
| 빠른 재시전 (`magic.frost.speed`) | 루트 · 부모 단계 조건 없음 | 얼음 위력 (`magic.frost.damage`) | — |
| 추가 얼음 조각 (`magic.frost.count`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 빠른 재시전 (`magic.frost.speed`) | — |
| 빠른 공전 (`magic.frost.orbitSpeed`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 추가 얼음 조각 (`magic.frost.count`) | — |
| 넓은 궤도 (`magic.frost.range`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 빠른 공전 (`magic.frost.orbitSpeed`) | 빠른 재시전 (`magic.frost.speed`) |
| 큰 얼음 조각 (`magic.frost.size`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 넓은 궤도 (`magic.frost.range`) | 얼음 위력 (`magic.frost.damage`) |
| 짙은 냉기 (`magic.frost.chill`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 큰 얼음 조각 (`magic.frost.size`) | — |
| 오래가는 빙결 (`magic.frost.freezeDuration`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 짙은 냉기 (`magic.frost.chill`) | 넓은 궤도 (`magic.frost.range`) |
| 눈보라 해금 (`magic.frost.blizzardUnlock`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 루트 | — |
| 눈보라 눈보라 반경 (`magic.frost.blizzardRange`) | 눈보라 해금 (`magic.frost.blizzardUnlock`) · 1단계 | 눈보라 위력 (`magic.frost.blizzardPower`) | — |
| 눈보라 눈보라 수 (`magic.frost.blizzardCount`) | 눈보라 해금 (`magic.frost.blizzardUnlock`) · 1단계 | 눈보라 눈보라 반경 (`magic.frost.blizzardRange`) | — |
| 눈보라 추가 냉기 (`magic.frost.blizzardCold`) | 눈보라 해금 (`magic.frost.blizzardUnlock`) · 1단계 | 눈보라 눈보라 수 (`magic.frost.blizzardCount`) | 눈보라 위력 (`magic.frost.blizzardPower`) |
| 쇄빙 파동 해금 (`magic.frost.shatterUnlock`) | 눈보라 해금 (`magic.frost.blizzardUnlock`) · 1단계 | 루트 | — |
| 쇄빙 파동 파동 반경 (`magic.frost.shatterRange`) | 쇄빙 파동 해금 (`magic.frost.shatterUnlock`) · 1단계 | 쇄빙 파동 위력 (`magic.frost.shatterPower`) | — |
| 쇄빙 파동 파편 대상 (`magic.frost.shatterShards`) | 쇄빙 파동 해금 (`magic.frost.shatterUnlock`) · 1단계 | 쇄빙 파동 파동 반경 (`magic.frost.shatterRange`) | — |
| 쇄빙 파동 연쇄 파쇄 (`magic.frost.shatterCascade`) | 쇄빙 파동 해금 (`magic.frost.shatterUnlock`) · 1단계 | 쇄빙 파동 파편 대상 (`magic.frost.shatterShards`) | 쇄빙 파동 위력 (`magic.frost.shatterPower`) |
| 이중 얼음 궤도 (`magic.frost.rings`) | 얼음 위력 (`magic.frost.damage`) · 1단계 | 오래가는 빙결 (`magic.frost.freezeDuration`) | 빠른 공전 (`magic.frost.orbitSpeed`) |

## 별도 목표 라인

스테이지·보스 목표 노드 4개는 성장 노드와 별도로 현재 코드에서 전체를 미리 보여주도록 되어 있다. 위 성장 노드 불일치 수와 진행 정체 수에는 넣지 않았다.

- 1 · 초원 (`GOAL_STAGE_01`)
- 고목 수호자 (`GOAL_BOSS_01`)
- 2 · 석재 던전 (`GOAL_STAGE_02`)
- 골석 수호자 (`GOAL_BOSS_02`)

원본 측정 데이터: [growth-tree-visibility-audit-2026-10-27.json](growth-tree-visibility-audit-2026-10-27.json).
