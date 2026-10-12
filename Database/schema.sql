-- =====================================================================
-- Liar's Bar 서버 DB 스키마
-- 실행 방법: HeidiSQL에서 root 세션으로 이 파일을 열고 F9 (전체 실행)
--   * liarsbar_server 계정은 테이블 생성 권한이 없으므로 반드시 root로 실행
--   * IF NOT EXISTS 라서 여러 번 실행해도 기존 테이블/데이터는 그대로
-- =====================================================================

USE liarsbar;

-- ---------------------------------------------------------------------
-- 1. users : 계정 (닉네임 = 로그인 ID)
--    비밀번호는 원문을 저장하지 않는다. 서버에서 PBKDF2로 만든 해시와 솔트만 저장.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    user_id        INT UNSIGNED   NOT NULL AUTO_INCREMENT,
    nickname       VARCHAR(12)    NOT NULL,            -- ServerConfig 닉네임 최대 길이와 맞춤
    password_hash  VARBINARY(32)  NOT NULL,            -- PBKDF2-SHA256 결과 32바이트
    password_salt  VARBINARY(16)  NOT NULL,            -- 계정마다 다른 랜덤 16바이트
    created_at     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at  DATETIME       NULL,
    PRIMARY KEY (user_id),
    UNIQUE KEY uq_users_nickname (nickname)            -- 같은 닉네임 가입 불가
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 2. user_stats : 누적 전적 (로비에서 빠르게 보여주기용 집계 테이블)
--    지금 서버가 알 수 있는 값(판 수, 승리)만. 필요하면 나중에 컬럼 추가.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_stats (
    user_id       INT UNSIGNED  NOT NULL,
    games_played  INT UNSIGNED  NOT NULL DEFAULT 0,
    wins          INT UNSIGNED  NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id),
    CONSTRAINT fk_stats_user FOREIGN KEY (user_id)
        REFERENCES users (user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 3. matches : 한 판의 기록
--    게임 도중에는 DB를 건드리지 않고, 끝났을 때 한 번에 INSERT 한다.
--    (started_at 은 서버가 메모리에 들고 있다가 같이 저장)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS matches (
    match_id        INT UNSIGNED  NOT NULL AUTO_INCREMENT,
    started_at      DATETIME      NOT NULL,
    ended_at        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    winner_user_id  INT UNSIGNED  NULL,                -- 승자 없이 끝나면(-1) NULL
    PRIMARY KEY (match_id),
    CONSTRAINT fk_matches_winner FOREIGN KEY (winner_user_id)
        REFERENCES users (user_id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 4. match_players : 그 판에 누가 몇 번 좌석으로 참가했는지 (중간 이탈자 포함)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS match_players (
    match_id  INT UNSIGNED      NOT NULL,
    user_id   INT UNSIGNED      NOT NULL,
    seat      TINYINT UNSIGNED  NOT NULL,
    PRIMARY KEY (match_id, user_id),                   -- 한 판에 같은 사람은 한 번만
    CONSTRAINT fk_mp_match FOREIGN KEY (match_id)
        REFERENCES matches (match_id) ON DELETE CASCADE,
    CONSTRAINT fk_mp_user FOREIGN KEY (user_id)
        REFERENCES users (user_id) ON DELETE CASCADE
) ENGINE=InnoDB;
