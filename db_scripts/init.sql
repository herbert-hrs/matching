CREATE SCHEMA matching
    AUTHORIZATION sl_aws;

ALTER SCHEMA public
    OWNER TO sl_aws;

CREATE TABLE PUBLIC.INSTRUMENT
(
	INSTRUMENT_ID VARCHAR(100) NOT NULL,
    INSTRUMENT_TYPE CHAR NOT NULL,
    SUB_TYPE CHAR NOT NULL,
    ISIN_PAPER VARCHAR(50) NOT NULL,
    SECURITY_GROUP VARCHAR(50) NULL,
	EMISSION_DATE DATE NOT NULL,
    EXPIRE_DATE DATE NOT NULL,
    EMISSION_TAX VARCHAR(50) NOT NULL,
    DESCRIPTION VARCHAR(500) NOT NULL,	
	EMITTER VARCHAR(500) NOT NULL,
	OLD_INTEREST_RATE VARCHAR(50) NOT NULL,
	NEW_INTEREST_RATE VARCHAR(50) NOT NULL,
	MULTIPLIER_PERCENTAGE VARCHAR(50) NOT NULL,
	INDEX_PERCENTAGE VARCHAR(50) NOT NULL,
    LAST_UPDATE DATE DEFAULT CURRENT_DATE NOT NULL
);

ALTER TABLE PUBLIC.INSTRUMENT
ADD CONSTRAINT PK_INSTRUMENT PRIMARY KEY(INSTRUMENT_ID);

INSERT INTO public.instrument(
	instrument_id, instrument_type, sub_type, isin_paper, security_group, emission_date, expire_date, emission_tax, description, emitter, old_interest_rate, new_interest_rate, multiplier_percentage, index_percentage, last_update)
	VALUES ('TESTE1-SL', 'T', '0', 'TESTE1-SL', 'GROUP','2021-12-20', '2021-12-20', '', '', '', '', '', '', '', '2021-12-20');
INSERT INTO public.instrument(
	instrument_id, instrument_type, sub_type,isin_paper, security_group, emission_date, expire_date, emission_tax, description, emitter, old_interest_rate, new_interest_rate, multiplier_percentage, index_percentage, last_update)
	VALUES ('TESTE2-SL', '', '1', 'TESTE2-SL', 'GROUP', '2021-12-20', '2021-12-20', '', '', '', '', '', '', '', '2021-12-20');
INSERT INTO public.instrument(
	instrument_id, instrument_type, sub_type,isin_paper, security_group, emission_date, expire_date, emission_tax, description, emitter, old_interest_rate, new_interest_rate, multiplier_percentage, index_percentage, last_update)
	VALUES ('TESTE3-SL', '', '0', 'TESTE3-SL', 'GROUP', '2021-12-20', '2021-12-20', '', '', '', '', '', '', '', '2021-12-20');
INSERT INTO public.instrument(
	instrument_id, instrument_type, sub_type,isin_paper, security_group, emission_date, expire_date, emission_tax, description, emitter, old_interest_rate, new_interest_rate, multiplier_percentage, index_percentage, last_update)
	VALUES ('TESTE4-SL', '2', '', 'TESTE4-SL', NULL, '2021-12-20', '2021-12-20', '', '', '', '', '', '', '', '2021-12-20');

CREATE TABLE MATCHING.MATCH_OFFER
(
   ID BIGSERIAL PRIMARY KEY,
   SYMBOL VARCHAR(30) NOT NULL,
   MESSAGE VARCHAR(700) NOT NULL,
   TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
   COUNT INT NOT NULL
);

CREATE TABLE DAILY_CLEANUP
(
    LAST_UPDATE DATE DEFAULT CURRENT_DATE - 1
);

CREATE TABLE MATCHING.INCREMENTAL_HISTORY
(
   ID BIGSERIAL PRIMARY KEY,
   MESSAGE VARCHAR(1000) NOT NULL,
   TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);