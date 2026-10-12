# 서버 파트 팀 프로젝트 이관 체크리스트

로컬 연습 프로젝트(`LiarsBar_Pre`)의 서버 · DB · 네트워크 작업을 팀 레포로 옮기는 순서.

## 0. 기준 버전

| 항목 | 버전 |
|---|---|
| Unity | 6000.2.8f1 |
| Mirror | 96.0.1 (KcpTransport) |
| NuGetForUnity | git URL 설치 (`https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`) |
| MySqlConnector | 2.6.2 (+ Logging.Abstractions 8.0.2, DependencyInjection.Abstractions 8.0.2, DiagnosticSource 8.0.1) |
| MariaDB | 10.5.18 |

팀 프로젝트의 Unity · Mirror 버전이 다르면 먼저 맞출 것.

## 1. 로컬 프로젝트에서 준비 (이관 전 1회)

- [x] 스크립트 UTF-8(BOM) 변환, `namespace LiarsBar.Server` 적용
- [x] 프로젝트 루트 `.editorconfig` 추가 (VS가 CP949로 저장하지 않게)
- [x] Unity Project 창에서 `Assets/3.Script/Server/` 폴더를 만들고 서버 스크립트 13개를 **드래그로** 이동 (탐색기 이동 금지: .meta 연결 유지)
- [x] 씬의 NetworkManager 오브젝트를 `Assets/2.Model/Prefabs/NetworkManager.prefab` 으로 저장
- [ ] Assets → Export Package
  - 체크: `3.Script/Server/`, `11.Config/ServerConfig.asset`, `2.Model/Prefabs/NetworkManager.prefab`
  - **Include dependencies 해제** (Mirror · 씬 · Player 프리팹이 딸려 들어가지 않게)
  - 파일명 예: `LiarsBar_Server_yyyyMMdd.unitypackage`

## 2. 팀 프로젝트에서 받기

- [ ] Mirror 96.0.1 import (이미 있으면 버전만 확인)
- [ ] NuGetForUnity 설치 → `Assets/packages.config` 를 로컬 프로젝트 것으로 교체 → NuGet → Restore Packages
- [ ] `.unitypackage` 더블클릭으로 import
- [ ] 프로젝트 루트에 `Database/` 폴더 통째로 복사 (`schema.sql`, `db.local.example.json`, `.gitignore`, 이 문서)
- [ ] 프로젝트 루트에 `.editorconfig` 복사
- [ ] `db.local.json` 은 **복사하지 않는다** → 각자 `db.local.example.json` 을 복사해 이름을 바꾸고 자기 비밀번호 입력

## 3. 씬 · 프리팹 연결

- [ ] 팀 씬에서 기존 NetworkManager / NetworkManagerHUD 제거 후 `NetworkManager.prefab` 배치
- [ ] Liars Network Manager → Player Prefab: 팀의 Player 프리팹 연결
- [ ] 팀 Player 프리팹에 `NetworkIdentity`, `PlayerProfile` 컴포넌트 추가 (프리팹 소유자인 플레이어 담당에게 요청)
- [ ] Seat Spawn Points: 팀 씬의 좌석 위치 0~3 연결 (원형 순서 = 턴 순서)
- [ ] Headless Start Mode = Do Nothing 확인
- [ ] ServerConfig: Min Players To Start (개발 2 / 실제 4), Use Database 확인

## 4. DB 준비 (각자 PC)

- [ ] MariaDB 설치 → root로 `liarsbar` DB와 `liarsbar_server@localhost` 계정 생성
  ```sql
  CREATE DATABASE liarsbar CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
  CREATE USER 'liarsbar_server'@'localhost' IDENTIFIED BY '비밀번호';
  GRANT SELECT, INSERT, UPDATE, DELETE ON liarsbar.* TO 'liarsbar_server'@'localhost';
  ```
- [ ] root로 `Database/schema.sql` 실행
- [ ] DB 없이 테스트할 팀원은 ServerConfig의 Use Database 끄기 (닉네임만으로 로그인)

## 5. 팀에 공유할 계약

- 모든 서버 클래스는 `LiarsBar.Server` 네임스페이스 → 사용하는 쪽에 `using LiarsBar.Server;`
- 로그인: `Auth.Login(nick, password, (ok, msg) => ...)` (비밀번호 인자 추가됨)
- 준비: `Lobby.SetReady(bool)`
- 접속: `LiarsNetworkManager.Instance.StartHostSafely()` / `StartServerSafely()` / `StartClient()` (Mirror 기본 StartHost를 직접 부르지 말 것: DB 확인을 건너뜀)
- 로딩 표시: `IsPreparingServer`, `NetworkClient.active && !NetworkClient.isConnected`
- 로비 표시: `PlayerProfile.Local`, `PlayerProfile.All`, `Seat` / `Nickname` / `IsLoggedIn` / `IsReady`
- 게임 연결: `IGameFlow` (`StartGame(int[] seats)`, `HandleDisconnect(int seat)`, `event Action<int> GameOver`) → 플레이어 담당 GameManager가 구현하면 `DummyGameFlow` 제거

## 6. 이관 후 확인

- [ ] 콘솔 에러 없음
- [ ] Host → `[DB] 준비 완료` → 좌석 0 배정
- [ ] Client 접속 → 좌석 1, 로그인 → 준비 → 시작 → 종료 → `[DB] 전적 저장 완료`
