// Zeitpläne: in esERP geänderte Kunden, Lieferanten und Artikel (SAP_CHANGED = NULL) alle 10 Minuten aus SAP neu übertragen.
// Datenbank: Prod-ES-Worker, Collection: Jobs-Schedules (Format siehe Meesenburg.ES.ApiService/Models/JobSchedule.cs).
//
// Ablauf:
// 1. Erst einfügen (Enabled = false), dann ESData-API, SAP-API und Worker deployen
//    und Meesenburg.ES.Jobs.Contracts 1.0.9 veröffentlichen.
// 2. Danach mit dem Befehl unten aktivieren.
// Start versetzt (0 / 3 / 6 Minuten), damit die drei Jobs nicht gleichzeitig auf SAP zugreifen.

const now = new Date();
const minutes = (m) => new Date(now.getTime() + m * 60 * 1000);

const schedule = (jobType, startOffsetMinutes) => ({
    Enabled: false,
    JobType: jobType,                       // 1 = Customer, 2 = Vendor, 3 = Article
    FilterJson: '{"eserpChangedOnly":true}',
    Priority: 5,
    ScheduleKind: 0,                        // Interval
    IntervalSeconds: 600,                   // 10 Minuten
    DailyLocalTime: null,
    TimeZoneId: null,
    PayloadJson: null,
    NextRunUtc: minutes(startOffsetMinutes),
    LastDispatchedUtc: null,
    LeaseToken: null,
    LeaseUntilUtc: null,
    LastError: null
});

db.getCollection("Jobs-Schedules").insertMany([
    schedule(1, 0),   // Kunden
    schedule(2, 3),   // Lieferanten
    schedule(3, 6)    // Artikel
]);

// Kontrolle
db.getCollection("Jobs-Schedules").find({ FilterJson: '{"eserpChangedOnly":true}' });

// Aktivieren (nach dem Deployment) – wieder um 0 / 3 / 6 Minuten versetzt
// [[1, 0], [2, 3], [3, 6]].forEach(([jobType, offset]) =>
//     db.getCollection("Jobs-Schedules").updateOne(
//         { FilterJson: '{"eserpChangedOnly":true}', JobType: jobType },
//         { $set: { Enabled: true, NextRunUtc: new Date(Date.now() + offset * 60 * 1000) } }
//     ));
