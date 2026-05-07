Use this prompt in the Flutter project:

```text
Integrate the customer order chat with an existing Firestore contract used by my ASP.NET POS/pharmacist backend.

Requirements:
1. Do not change the Firestore collection paths.
2. The customer app must read orders from `orders` where `customerUid == auth.currentUser!.uid`.
3. The customer app must read chat messages from `orders/{orderId}/messages`.
4. The customer app must write customer messages into `orders/{orderId}/messages`.
5. The pharmacist/POS already reads and writes into the same subcollection, so preserve compatibility exactly.

Order document contract:
- Collection: `orders`
- Firestore document id: the existing order id already used by the app, for example `order_1777731293205`
- Expected fields:
  - `customerUid`: string
  - `referenceNumber` or `reference_number` or `orderReference`: string
  - `status` or `orderStatus`: string
  - `pharmacyName` or `pharmacy.name`: string
  - `pharmacistUid` or `pharmacist.uid`: string
  - `pharmacistName` or `pharmacist.name`: string
  - `requiresPrescription` or `prescriptionRequired`: bool
  - `createdAt`: timestamp
  - `updatedAt`: timestamp
  - optional `lastMessageAt`: timestamp

Message document contract:
- Collection: `orders/{orderId}/messages`
- One document per message
- Fields:
  - `type`: `"text"`
  - `text`: string
  - `orderId`: string matching the parent order document id
  - `orderReference`: string
  - `senderUid`: string
  - `senderRole`: `"customer"` for customer messages, `"pharmacist"` for pharmacist replies, `"system"` for system notices
  - `senderName`: string
  - `recipientRole`: `"pharmacist"` for customer messages, `"customer"` for pharmacist replies
  - `createdAt`: server timestamp

Implement:
1. A typed `OrderChatMessage` model and an `OrderChatThread`/`OrderSummary` model.
2. A Firestore repository/service that:
   - streams the signed-in customer’s orders
   - streams messages for a given order from `orders/{orderId}/messages` ordered by `createdAt`
   - sends a customer message with:
     - `type: "text"`
     - `text`
     - `orderId`
     - `orderReference`
     - `senderUid: auth.currentUser!.uid`
     - `senderRole: "customer"`
     - `senderName`: current customer display name
     - `recipientRole: "pharmacist"`
     - `createdAt: FieldValue.serverTimestamp()`
   - updates the parent order doc with `lastMessageAt: FieldValue.serverTimestamp()` after sending
3. A chat screen that:
   - lists customer orders
   - opens a selected order thread
   - renders customer messages on the right and pharmacist/system messages on the left
   - shows pharmacist name and pharmacy name from the parent order doc
   - handles empty states and loading states cleanly
4. Null-safe Dart code using `StreamBuilder`, `cloud_firestore`, and `firebase_auth`.
5. Keep the code modular: models, repository/service, and UI widgets separated.

Important:
- Do not create a different collection like `chats`, `threads`, or `messages` at the root.
- Do not overwrite existing pharmacist/customer messages.
- Use Firestore server timestamps.
- Assume the POS backend already writes pharmacist replies into the same `orders/{orderId}/messages` subcollection.

Deliver:
1. The Dart models
2. The Firestore service/repository
3. The chat screen/widgets
4. Any helper mappers/parsers needed for alternate field names like `referenceNumber`, `reference_number`, `orderReference`, `status`, and `orderStatus`
```
