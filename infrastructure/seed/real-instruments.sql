BEGIN;

LOCK TABLE "Instruments" IN SHARE ROW EXCLUSIVE MODE;

DO $seed$
DECLARE
    listing record;
BEGIN
    FOR listing IN
        SELECT * FROM (VALUES
            ('20000000-0000-0000-0000-000000000001'::uuid, 'AAPL', 'Apple Inc.'),
            ('20000000-0000-0000-0000-000000000002'::uuid, 'MSFT', 'Microsoft Corporation')
        ) AS listings(id, symbol, name)
    LOOP
        IF EXISTS (
            SELECT 1 FROM "Instruments"
            WHERE ("Id" = listing.id AND ("ExchangeMic", "Symbol") IS DISTINCT FROM ('XNAS', listing.symbol))
               OR (("Id" = listing.id OR ("ExchangeMic" = 'XNAS' AND "Symbol" = listing.symbol))
                   AND ("IsSample" OR "Currency" <> 'USD'))
        ) THEN
            RAISE EXCEPTION 'Real listing setup conflicts with an existing identity, currency or sample flag.';
        END IF;
        INSERT INTO "Instruments" ("Id", "Symbol", "ExchangeMic", "Name", "Currency", "IsSample")
        VALUES (listing.id, listing.symbol, 'XNAS', listing.name, 'USD', false)
        ON CONFLICT ("ExchangeMic", "Symbol") DO NOTHING;
    END LOOP;
END
$seed$;

COMMIT;
