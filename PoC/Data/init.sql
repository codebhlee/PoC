-- movies 테이블
CREATE TABLE IF NOT EXISTS movies (
    movie_id   INTEGER PRIMARY KEY,
    title      TEXT    NOT NULL,
    genres     TEXT,
    embedding  REAL[]  -- 384차원 float 벡터
);

-- links 테이블
CREATE TABLE IF NOT EXISTS links (
    movie_id   INTEGER PRIMARY KEY REFERENCES movies(movie_id),
    imdb_id    TEXT,
    tmdb_id    INTEGER
);

-- ratings 테이블
CREATE TABLE IF NOT EXISTS ratings (
    user_id    INTEGER NOT NULL,
    movie_id   INTEGER NOT NULL REFERENCES movies(movie_id),
    rating     NUMERIC(2,1) NOT NULL,
    ts         BIGINT,
    PRIMARY KEY (user_id, movie_id)
);

-- tags 테이블
CREATE TABLE IF NOT EXISTS tags (
    user_id    INTEGER NOT NULL,
    movie_id   INTEGER NOT NULL REFERENCES movies(movie_id),
    tag        TEXT    NOT NULL,
    ts         BIGINT,
    PRIMARY KEY (user_id, movie_id, tag)
);

-- CSV 임포트 (컨테이너 내부 /data 경로)
COPY movies  (movie_id, title, genres)              FROM '/data/movies.csv'  CSV HEADER;
COPY links   (movie_id, imdb_id, tmdb_id)           FROM '/data/links.csv'   CSV HEADER;
COPY ratings (user_id, movie_id, rating, ts)        FROM '/data/ratings.csv' CSV HEADER;
COPY tags    (user_id, movie_id, tag, ts)           FROM '/data/tags.csv'    CSV HEADER;
