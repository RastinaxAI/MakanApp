# Validation & provenance

- Source repository: `RastinaxAI/MakanApp`
- Pinned HEAD: `49cb86ca897301c80c771d170825c1aec4a99226`
- Catalog document pinned with HEAD: `docs/backend-api-reference.md`
- Runtime snapshot advertised by document: `ef6762462bda4415a3d052454fcf53af7fa6b999`
- Parsed HTTP operations: **325**; distinct method+route: **325**.
- Distinct literal paths: **290**.
- Controllers: **57**; schema headings: **551**.
- Parsed JSON-request operations: **136**; multipart: **1**; body-less: **188**.
- Anonymous operations: **3** (from API reference authorization metadata).
- Mutations blocked until opt-in: **187** HTTP non-GET methods + **1** stateful GET.
- Contract extraction performed without calling a live backend, provider, SQL or SMS.
- Health endpoints and SignalR Hub are documented separately and *not* counted in the 325 Controller operations.
- Postman tests check explicit expected status, top-level OpenAPI-required response fields when the response is JSON, and optional response time. They do **not** prove deep business-state correctness automatically.
- The compiled/dynamic live OpenAPI comparison and production E2E gate remain **NOT_RUN**.
- Some AI contract DTOs changed after documented snapshot; code at pinned HEAD wins when dynamically regenerated.
