# 01: Secure Notifications API, Make Hearing Reminders Idempotent & Localized, and Test the Notification Slice End-to-End

**What to build:** Ensure the Notifications API requires authentication across all endpoints, make the 1-day-prior hearing reminder background job (`HearingReminderJob`) generate localized Arabic reminder messages (`موعد الجلسة` / `رقم القضية` / `عنوان القضية` / `نوع الجلسة`) idempotently so multiple runs on the same day never create duplicate notifications for the same hearing, and cover the entire Notifications slice (background job, API controller, and `NotificationBell` UI) with automated tests.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] `NotificationsController` requires authentication (`[Authorize]`) so unauthenticated callers cannot read or mutate hearing notifications or push subscriptions.
- [x] `HearingReminderJob` formats reminder titles and messages in Arabic using the case number (`رقم القضية`), case title (`عنوان القضية`), hearing date (`موعد الجلسة`), and optional purpose (`نوع الجلسة`), and skips creating a duplicate `Notification` if an unread/existing reminder for the same `(CaseId, HearingDate)` was already created.
- [x] Automated unit tests cover `HearingReminderJob` (creating reminders for tomorrow's hearings, ignoring soft-deleted or other-day hearings, and idempotency when executed twice on the same day) and `NotificationsController` (listing notifications, unread count, marking single/all as read, and push subscription idempotency).
- [x] Automated bUnit tests cover `NotificationBell` (displaying unread count badge, opening dropdown, marking a notification as read and navigating to the linked Case, and marking all notifications as read).
