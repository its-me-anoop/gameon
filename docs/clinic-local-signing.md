# Clinic archive signing with an existing local identity

This path compiles once on the released hosted Xcode, signs that compiled archive on the Mac with its existing Apple Development key, and adopts the sealed result for normal TestFlight promotion. The private signing key stays in the local keychain. No helper creates or revokes certificates, exports private keys, registers devices, or changes Apple account settings.

The ordinary automatic-signing workflow remains available with `local_signing=false`. The local route has three separate dispatches: unsigned build, signed adoption, then promotion after the physical check. Unsigned build/adoption dispatches reject `upload_to_testflight=true`.

## Prepare the frozen source

Commit the final Unity source, export/packaging tools, these signing helpers and workflow together. Produce the production Device SDK Unity export and private transfer using the existing release procedure. Pin the full source SHA and export SHA256. The unsigned workflow run's actual `head_sha` must equal this source SHA; dispatch from the frozen branch/commit, before moving it again.

All examples below use shell variables populated from verified receipts. They are placeholders, not candidate IDs:

```sh
gh workflow run release.yml --ref main \
  -f source_sha="$CLINIC_SOURCE_SHA" \
  -f export_release_tag="$CLINIC_EXPORT_TAG" \
  -f export_sha256="$CLINIC_EXPORT_SHA256" \
  -f local_signing=true -f upload_to_testflight=false
```

Record the successful run ID as `CLINIC_UNSIGNED_RUN_ID`. Download the distinctly named unsigned artifact to a new directory:

```sh
gh run download "$CLINIC_UNSIGNED_RUN_ID" \
  --name "unity-ios-unsigned-archive-$CLINIC_SOURCE_SHA" \
  --dir "$CLINIC_UNSIGNED_DIR"
```

The artifact contains `OrbitOrchard.unsigned.xcarchive.zip` and `unsigned-archive-manifest.json`. Pin both SHA256 values against the unsigned seal step's output as `CLINIC_UNSIGNED_SHA256` and `CLINIC_UNSIGNED_MANIFEST_SHA256`. An unsigned artifact cannot substitute for the signed artifact name or contract.

## Preflight and sign locally

Choose the existing Apple Development keychain identity and its matching active development profile. Record the public certificate's DER SHA256 and the registered device UDID's UTF-8 SHA256 as `CLINIC_SIGNER_SHA256` and `CLINIC_DEVICE_SHA256`. These fingerprints identify public signing material and the exact allowed test device; they are not private keys. The profile must authorize the correct team/bundle, Game Center and that device.

The known audit candidate was `Apple Development: Anoop Jose (8R89HTA583)` with “Little Lifeline Clinic QA 2026-09-12”. Recheck validity and the exact fingerprint when using it; do not select a revoked API-generated identity.

```sh
CLINIC_SIGN_ARGS=(
  sign
  --unsigned "$CLINIC_UNSIGNED_DIR/OrbitOrchard.unsigned.xcarchive.zip"
  --manifest "$CLINIC_UNSIGNED_DIR/unsigned-archive-manifest.json"
  --unsigned-sha256 "$CLINIC_UNSIGNED_SHA256"
  --manifest-sha256 "$CLINIC_UNSIGNED_MANIFEST_SHA256"
  --unsigned-run-id "$CLINIC_UNSIGNED_RUN_ID"
  --source "$CLINIC_SOURCE_SHA"
  --export-sha256 "$CLINIC_EXPORT_SHA256"
  --identity "$CLINIC_SIGNING_IDENTITY"
  --profile "$CLINIC_PROFILE_PATH"
  --signer-sha256 "$CLINIC_SIGNER_SHA256"
  --device-sha256 "$CLINIC_DEVICE_SHA256"
  --output "$CLINIC_SIGNED_DIR"
)
python3 Tools/clinic_archive.py "${CLINIC_SIGN_ARGS[@]}"
```

The default is preflight: verification uses temporary extraction directories, produces no persistent output, and performs no signing. After reviewing that result, the release operator can execute the same command with `--write`:

```sh
python3 Tools/clinic_archive.py "${CLINIC_SIGN_ARGS[@]}" --write
```

Signing preserves app/framework resource plists and their hosted `DTXcodeBuild`, SDK and `BuildMachineOSBuild` stamps. The helper signs nested code first, adds the existing profile, then signs the app with explicit development/Game Center entitlements. It sets only the outer archive's truthful signing identity/team fields. It never runs a local build or export.

Outputs are a locally signed `.xcarchive`, `OrbitOrchard.xcarchive.zip`, `signing-receipt.json` and the two-file `clinic-signed-transfer.zip`. Existing output directories/files are never overwritten. A failed attempt can leave an incomplete directory; choose a new output path after resolving the failure. An incomplete attempt has no valid adoption contract.

## Adopt the exact signed archive

Create a new private draft release tag prefixed `clinic-signed-`, targeting the exact source SHA. Upload only `clinic-signed-transfer.zip`; retain the draft state and never use an overwrite/clobber option. Pin its printed SHA256 as `CLINIC_SIGNED_SHA256`.

```sh
gh release create "$CLINIC_SIGNED_TAG" --target "$CLINIC_SOURCE_SHA" \
  --draft --title 'Private Clinic signing transfer' \
  --notes 'Private transport for the sealed Clinic archive.'
gh release upload "$CLINIC_SIGNED_TAG" "$CLINIC_SIGNED_DIR/clinic-signed-transfer.zip"
gh workflow run release.yml --ref main \
  -f source_sha="$CLINIC_SOURCE_SHA" \
  -f export_sha256="$CLINIC_EXPORT_SHA256" \
  -f unsigned_run_id="$CLINIC_UNSIGNED_RUN_ID" \
  -f signed_release_tag="$CLINIC_SIGNED_TAG" \
  -f signed_sha256="$CLINIC_SIGNED_SHA256" \
  -f signer_sha256="$CLINIC_SIGNER_SHA256" \
  -f device_sha256="$CLINIC_DEVICE_SHA256" \
  -f upload_to_testflight=false
```

Adoption verifies the successful source run and private release target, downloads the unsigned counterpart directly from that run, and independently compares both archives. All ordinary resources, app/framework plists and dSYMs must match byte-for-byte. Mach-O comparison retains every protected byte at its original offset and permits only narrowly validated signature-command changes, derived zero padding and exact signature-related `__LINKEDIT` growth. It checks actual payload hashes, not just executable UUIDs. Separate `codesign` checks verify signatures, every code object's pinned signer, and exact entitlements.

The adoption helper makes authenticated **GET requests only** to App Store Connect. It requires the embedded profile bytes to match Apple's active development profile, the pinned certificate to remain related to that profile, and the pinned test device to remain enabled. It uses the existing ASC secrets in the hosted job; it does not import the personal development key there.

A successful adoption publishes `unity-ios-archive-<source SHA>` with the unchanged signed ZIP, manifest and signing/adoption receipts. Record its run ID as `CLINIC_ADOPTION_RUN_ID`.

## Physical check and promotion

Download the adopted signed artifact and verify its ZIP checksum matches both the adoption manifest and the local signing receipt. Install the app extracted from that sealed archive without rebuilding or re-signing it. Preserve the device's app data and record the bounded physical check against this archive SHA256. Keep simulator, source/test and physical-device evidence distinct.

After the physical acceptance check, promote that same archive:

```sh
gh workflow run release.yml --ref main \
  -f source_sha="$CLINIC_SOURCE_SHA" \
  -f archive_run_id="$CLINIC_ADOPTION_RUN_ID" \
  -f upload_to_testflight=true
```

Promotion verifies the producing workflow run, source SHA, manifest run ID, signed ZIP checksum and both adoption receipts before safe ZIP extraction and signature verification. The existing released-Xcode `app-store-connect` export/upload follows without recompilation. Verify App Store Connect processing and internal TestFlight availability independently after transport succeeds.

## Validation and operating limits

```sh
python3 -B -m unittest discover -s Tools/tests -p 'test_clinic_*.py'
```

The signing comparator intentionally supports only little-endian thin arm64 ALL executables/dylibs with a terminal read-only `__LINKEDIT`. It rejects arm64e, FAT binaries, encryption, repacking, unexpected resources/symlinks, inadequate header padding and payload changes. Expand the design and tests before changing those limits.

The first full Unity unsigned→local-sign→adoption→hosted-export path remains a runtime acceptance check. In particular, automatic App Store export of the locally signed archive under hosted Xcode 26.6 has not been established by the unit tests. Local Xcode 27's help permits automatic distribution of manually signed archives, while older Apple web help describes a restriction. Preserve the sealed archive if hosted export fails; inspect that exact exporter's diagnostics rather than rebuilding, altering build stamps or retiring certificates.

This route adds one normal hosted compilation and one lightweight adoption job; promotion performs the existing export/upload. Retention is seven days for unsigned and signed archives, one day for intermediate signed transport. Rollback means stop before promotion and retain the immutable receipts; no account signing changes need undoing. The ordinary automatic-signing mode is still available, but its existing certificate-capacity limit is unchanged.

References: [Apple code-signing format and nested signing order](https://developer.apple.com/documentation/xcode/using-the-latest-code-signature-format), [Apple signing allocator](https://github.com/apple-oss-distributions/cctools/blob/main/misc/codesign_allocate.c), [Apple cloud-managed distribution certificates](https://developer.apple.com/help/account/certificates/cloud-managed-certificates).
