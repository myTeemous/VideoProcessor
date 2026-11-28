# Confirm copies the uploaded file out of reach of the Upload slot

An Upload slot's write-only SAS URL has to stay valid long enough for a multi-GB block upload (an hour by default), and a SAS URL can't be revoked once issued. If Confirm validated the blob in place, a User could confirm a small valid clip and then overwrite the same blob with an oversized or non-video file that is never checked again. So Users upload into an `uploads` container, and Confirm makes a service-side copy into a `videos` container, which no Upload slot URL can write. Confirm then measures and probes the copy, not the upload, and deletes the upload. A Rejected Video's copy is deleted too.

Confirm takes longer because of the copy, but a copy within one storage account never moves bytes through the Api. Blobs left behind by Confirms that never finished are in either container, so the cleanup sweep for Videos stuck in AwaitingUpload has to delete from both.

## Considered Options

- **A short Upload slot lifetime**: only narrows the window, and a short lifetime breaks slow multi-GB uploads.
- **A user delegation SAS revoked at Confirm**: revoking means rotating the user delegation key, which revokes every outstanding SAS at once, and the local Azurite stack doesn't support it.
- **Checking the blob's ETag before each later read**: every reader of the blob (downloads, the Worker) has to remember to check it, and a failed check leaves a Ready Video whose file can't be used.
