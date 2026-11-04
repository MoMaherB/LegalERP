# 04: Harden and Test Authentication, SuperAdmin User Management Guards, and User Profile Picture Lifecycle

**What to build:** Harden `ProfileController` so profile picture uploads use `DocumentOwnerTypes.Users` and delete superseded avatar files on disk when replaced, and add unit and bUnit tests covering `AuthController`, `UsersController` safety invariants, `ProfileController`, and `UserManagement`.

**Blocked by:** None (03 resolved)

**Status:** resolved

- [x] `ProfileController.UploadProfilePicture` uses `DocumentOwnerTypes.Users` and removes the previous profile picture file via `IFileStorageService.DeleteFileAsync` when an existing avatar is replaced.
- [x] Automated unit tests cover `AuthController` (blocking deactivated accounts from logging in and returning role claims for active accounts) and `ProfileController` (password confirmation validation, password change, and profile picture replacement cleanup).
- [x] Automated unit tests cover `UsersController` safety guards: preventing a `SuperAdmin` from changing their own role, deactivating themselves, or permanently deleting themselves, and preventing demotion/deactivation/deletion of the last active `SuperAdmin`.
- [x] Automated bUnit tests cover `UserManagement` (rendering user list with roles and active badges, toggling deactivation/reactivation, and permanent deletion confirmation).
