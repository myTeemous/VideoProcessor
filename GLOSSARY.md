# VideoProcessor

A service where users upload videos and run processing operations on them, producing new files they can download.

## Language

### Videos

**Video**:
A file a User uploaded, together with its metadata. Only Videos can be processed.
_Avoid_: Upload (as a noun), source file, media, asset

**Video status**:
Where a Video is in its lifecycle: **AwaitingUpload** (a slot was issued but no file has been confirmed), **Ready** (confirmed and validated, can be processed) or **Rejected** (confirmed but failed validation).

**Upload slot**:
Permission, issued to a User, to upload one file for one Video directly to storage within a short time window.

**Confirm**:
The User's declaration that the file for a Video has finished uploading, which triggers validation.

### Processing

**Operation**:
One kind of processing: Thumbnail extraction, Trim, Convert, Resize, Audio extraction or Compress.
_Avoid_: Task, action, transform

**Job**:
A User's request to run one Operation, with its parameters, on one Video. Its status is **Pending**, **Processing**, **Completed** or **Failed**.
_Avoid_: Task, request, work item

**Output**:
The single file a Completed Job produced. Outputs are not Videos and cannot themselves be processed.
_Avoid_: Result, processed video, rendition, derivative

### People

**User**:
A person with an account. Every Video, and therefore every Job and Output, belongs to exactly one User.
_Avoid_: Owner, customer, account
