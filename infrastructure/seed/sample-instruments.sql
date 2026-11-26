BEGIN;

-- Serialize imports with other writers so a skipped logical row cannot hide a UUID collision.
LOCK TABLE "Instruments" IN SHARE ROW EXCLUSIVE MODE;

DO $seed$
DECLARE
    sample record;
BEGIN
    FOR sample IN
        SELECT * FROM (VALUES
            ('10000000-0000-0000-0000-000000000001'::uuid, 'S-AO', 'XAAA', 'Alder Orbit (sample)', 'USD'),
            ('10000000-0000-0000-0000-000000000002'::uuid, 'S-BG', 'XAAA', 'Bracken Grid (sample)', 'USD'),
            ('10000000-0000-0000-0000-000000000003'::uuid, 'S-AO', 'XBBB', 'Alder Orbit alternate listing (sample)', 'EUR')
        ) AS samples(id, symbol, exchange_mic, name, currency)
    LOOP
        IF EXISTS (
            SELECT 1 FROM "Instruments"
            WHERE "Id" = sample.id
              AND ("ExchangeMic", "Symbol") IS DISTINCT FROM (sample.exchange_mic, sample.symbol)
        ) THEN
            RAISE EXCEPTION 'Sample import conflicts with an existing instrument identifier.';
        END IF;

        INSERT INTO "Instruments" ("Id", "Symbol", "ExchangeMic", "Name", "Currency", "IsSample")
        VALUES (sample.id, sample.symbol, sample.exchange_mic, sample.name, sample.currency, true)
        ON CONFLICT ("ExchangeMic", "Symbol") DO NOTHING;
    END LOOP;
END
$seed$;

COMMIT;
