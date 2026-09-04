# Prompt gửi Gemini/Antigravity

Bạn là bên trực tiếp điều tra và triển khai trong Unity project `E:/Unity/GameObject/Game3D/ProJectZomboiNhai`. Hãy xử lý đúng ba nhóm vấn đề dưới đây. Không sửa code/scene/prefab/asset ngoài phạm vi cần thiết.

## Quy tắc bắt buộc

1. Trước khi sửa, hãy đọc Git state, kiểm tra scene/prefab/script liên quan và tái hiện bằng Host + ít nhất một Client. Không đoán nguyên nhân.
2. Không xóa dữ liệu, không đổi tên/xóa audio asset, không đổi API public hoặc serialized reference không liên quan. Giữ nguyên Fusion authority/security semantics, damage/death state-authority và Save/Back/unsaved-change behavior hiện có trừ khi có lý do kỹ thuật rõ ràng.
3. Với jitter, phải instrument và xác định writer/timing/correction gây giật trước khi đổi smoothing. Không thêm `Lerp` tùy ý lên networked root, không ghi transform root từ `Update/LateUpdate`, và không bật đồng thời nhiều hệ thống interpolation.
4. Diagnostic log phải có guard bằng `UNITY_EDITOR` hoặc diagnostic toggle, có identity, authority, tick/frame và số đo; không để log spam trong build cuối.
5. Sau khi sửa, trả về: nguyên nhân gốc có bằng chứng, danh sách file đã đổi, thay đổi theo từng file, test commands/output, Unity Console/compile status, ảnh hoặc video kiểm chứng, và limitation còn lại.

## Bối cảnh đã rà soát

- `Assets/Script/Tin/PlayerCombat.cs` và `Assets/Script/Tin/PlayerHealth.cs` hiện dùng `GetComponentInChildren<Animator>()`.
- Trong `Assets/Prefab/Player.prefab`, child order có `MuzzleFlash` trước `Visual`. Animator của `MuzzleFlash` dùng `Assets/SmallScaleInt/2D Zombie interior Tile pack 1/Effects/GunFire/MuzzleFlash.controller`; Animator của `Visual` dùng `Assets/AnimationPlayer/Player.controller`.
- `Player.controller` có các parameter cần cho nhân vật: `GunBash`, `RandomBash`, `TakeDamage`, `IsDead` và các parameter movement khác. Muzzle controller không phải character controller.
- `PlayerMovement` đã có serialized animator reference trỏ tới `Visual`, nên binding giữa movement và combat/health hiện không nhất quán.
- `PlayerCombat.Bash()` hiện chỉ gọi `RPC_PlayBashAnimation` trong nhánh `HasStateAuthority && Runner.IsForward`, dù RPC khai báo source gồm StateAuthority và InputAuthority. Hãy thiết kế lại rõ ràng việc local prediction, authoritative replay và dedup cho host, owning client và observing client; không để trigger bị phát hai lần hoặc bị reset/skipped.
- `PlayerHealth` phát hit/death qua RPC StateAuthority -> All. Damage/death mutation phải tiếp tục state-authoritative; chỉ sửa event/presentation nếu cần.
- Cả hai player prefab đang dùng `NetworkRigidbody2D` với `Visual` là `_interpolationTarget` và Rigidbody2D interpolation tắt. `Assets/Scenes/Thai.unity` và `Assets/Scenes/Tin.unity` đang có `ClientPhysicsSimulation: 0`, còn `Assets/Prefab/Prototype Runner.prefab` có `ClientPhysicsSimulation: 2`. Hãy xác định scene/runner thực tế khi chạy multiplayer và kết luận mode đúng từ bằng chứng, không tự động copy giá trị của prefab mẫu.
- Người dùng vẫn thấy player giật lag trong multiplayer và xác nhận không phải do mạng. Cần kiểm tra toàn bộ writer vào root Rigidbody/Transform/Visual, Fusion tick vs Unity physics/render timing, client prediction/reconciliation, collision, spawn/respawn, culling và camera target.
- Audio settings dùng các key `GameMasterVolume`, `GameMusicVolume`, `GameSFXVolume`. `Assets/Script/Tin/MainMenuManager.cs` tạo slider Main Menu và Pause Menu riêng, dùng các temp field chung nhưng callback hiện không cập nhật paired slider live. `LoadSavedSettingsToTemp()` có set cả hai slider và áp dụng preview; `SaveSettings()` ghi PlayerPrefs. `GameplayMusicController`, `AutoUIManager`, `PlayerCombat`, `PlayerHealth` và `PlayerMovement` đang đọc volume qua nhiều đường khác nhau.

## 1. Multiplayer bash / hit / die animation

Điều tra và sửa để tất cả trường hợp sau đều chạy đúng:

- Host local avatar bash, bị hit, chết.
- Host nhìn thấy client bash, bị hit, chết.
- Client local avatar bash, bị hit, chết.
- Client nhìn thấy host bash, bị hit, chết.
- Client nhìn thấy client còn lại bash, bị hit, chết (nếu harness hỗ trợ).

Yêu cầu kỹ thuật:

- Tạo resolver deterministic hoặc serialized reference cho character `Visual` Animator ở cả `Player.prefab` và `Player2.prefab`; tuyệt đối không phụ thuộc child order và không lấy MuzzleFlash Animator.
- Validate runtime controller/path và sự tồn tại của các parameter `GunBash`, `RandomBash`, `TakeDamage`, `IsDead` trước khi trigger; không nuốt lỗi parameter silently.
- Kiểm tra `PlayerAnimationEventForwarder` và các animation event không bị gọi sai authority hoặc bị mất trên proxy.
- Bash phải có đúng policy: input phản hồi hợp lý ở owner nếu có prediction; hit/damage/death vẫn do StateAuthority quyết định; mọi observer nhận presentation event chính xác một lần. Không duplicate SFX, damage, death logic hoặc RPC replay.
- Không dùng RPC animation để tự ý thay đổi gameplay state ở proxy. Nếu cần networked state/sequence để late join hoặc tránh mất RPC, giải thích lý do và giới hạn field.
- Giữ đúng animator movement parameters hiện có và hỗ trợ cả hai character controller.

## 2. Player movement visual jitter, không quy cho network latency

Trước tiên thêm diagnostic tối thiểu để so sánh trong cùng thời gian:

- `Runner.Tick`, Unity frame, `HasStateAuthority`, `HasInputAuthority`, `Runner.IsForward`.
- root `transform.position`, `Rigidbody2D.position`, velocity.
- `NetworkRigidbody2D.InterpolationTarget` (`Visual`) position/local position.
- khoảng cách root-vs-Rigidbody-vs-Visual, correction delta/tần suất correction, và script/writer đang thay đổi chúng.

Kiểm tra các điểm sau:

- active scene có `RunnerSimulatePhysics2D`/`NetworkRunner` nào, `ClientPhysicsSimulation` thực tế là gì, tick/fixed timestep có nhất quán không;
- có duplicate `NetworkTransform`/`NetworkRigidbody2D`, Unity Rigidbody2D interpolation, `Update/LateUpdate/FixedUpdateNetwork/Render` cùng ghi một transform không;
- `PlayerMovement.FixedUpdateNetwork()` gán `rb.linearVelocity` mỗi tick có gây correction/oscillation trong mode đang chạy không;
- `NetworkRigidbody2D` có thực sự điều khiển child `Visual` như render-only target không;
- camera target có bám đúng interpolation target không;
- các chuyển trạng thái idle -> walk -> run -> stop, đổi hướng, aim/crouch, collision, spawn/respawn, vehicle có writer hoặc snap riêng không.

Sau khi có bằng chứng, sửa theo nguyên tắc:

- physics root là nguồn authoritative duy nhất;
- `Visual` là render/interpolation target duy nhất;
- không smoothing che giấu correction deterministic;
- không phá collision, hit detection, spawn/respawn, camera, animation event;
- không thêm latency cảm nhận được cho input local.

## 3. Phân loại sound và đồng bộ Music/SFX options

### 3a. Tạo inventory có căn cứ

Quét asset thật, inspector references, `Resources.Load`, `AudioClip`, `AudioSource`, playback call và event. Xuất bảng gồm: asset path, category, nơi dùng, local/remote/spatial policy, volume source, có dùng hay không.

Phân loại tối thiểu:

- Music/BGM: `Assets/Music/ThemeGamePlay.mp3`, `Assets/Resources/Sound/Project Zomboid Main Theme.mp3`, menu BGM được gán vào `AutoMainMenuManager.menuBGM`, và theme được gán qua `GameplayMusicSettings`.
- SFX: `Resources/Sound/UI`, `Actions`, `BodyState`, `Footsteps`, `Melee`, `Vehicles/Repair`, `Weapons/AK47`, `Weapons/S12K`, cùng zombie/action SFX tìm thấy trong code.
- Story/Voice: toàn bộ `Resources/Sound/Story/RouteB`; đây là thoại/phát thanh, không tự xếp vào BGM. Nếu hệ thống hiện chỉ có Music/SFX, ghi rõ nó đang đi theo master/music/SFX nào và không đổi hành vi âm thanh ngoài yêu cầu.
- Phân biệt file raw/duplicate/unused với clip runtime đang dùng; không xóa asset.

### 3b. Một source of truth cho volume

- Giữ compatibility với `GameMasterVolume`, `GameMusicVolume`, `GameSFXVolume` và default hiện tại.
- Tạo hoặc dùng một runtime settings service/model tập trung, có API/event rõ ràng cho `Master`, `Music`, `SFX`; không để Main Menu và Pause Menu giữ hai state độc lập.
- Main Menu và Pause Menu phải bind cùng model. Khi kéo slider ở Main Menu, slider và label tương ứng trong Pause Menu cập nhật; chiều ngược lại cũng vậy. Dùng guard/`SetValueWithoutNotify`/cơ chế tương đương để không recursion và không nhân callback.
- Giữ đúng semantics preview/save/revert hiện tại: thay đổi preview áp dụng nhất quán; Save ghi PlayerPrefs; Back/revert trả về saved values; mở lại panel không làm mất unsaved preview bất ngờ nếu UX hiện tại đang giữ preview trong cùng flow.
- Một hàm apply duy nhất phải cập nhật đúng `AudioListener.volume`, menu BGM, gameplay BGM và các nguồn SFX. Không để gameplay music/UI/player/weapon/melee/vehicle/zombie tự đọc key theo các cách mâu thuẫn.
- Music và SFX phải độc lập; master phải nhân/điều khiển cả hai. Kiểm tra cả runtime trước và sau scene transition, không chỉ giá trị slider.
- Không cần rewrite lớn sang AudioMixer nếu không cần; ưu tiên thay đổi nhỏ, tương thích scene/prefab và có test.

## Phạm vi file ưu tiên

Chỉ sửa nếu diagnosis chứng minh cần: `Assets/Script/Tin/PlayerCombat.cs`, `PlayerHealth.cs`, `PlayerMovement.cs`, `PlayerAnimationEventForwarder.cs`, `Assets/Script/Tin/Multiplayer/PlayerNetworkInput.cs`, `HostModeSpawner.cs`, `MainMenuManager.cs`, `GlobalSettingsManager.cs`, `GameplayMusicController.cs`, `AutoUIManager.cs`, `GameplayAudioSpatializer.cs`, hai player prefab, active gameplay runner/scene config, và test dưới `Assets/Script/Tin/Prototype/Tests`.

## Tiêu chí nghiệm thu và kiểm thử

- Unity compile không có lỗi mới.
- EditMode tests:
  - resolver chọn đúng `Visual` Animator, controller và parameters cho Player/Player2;
  - catalog audio không bỏ sót asset used, phân loại không mơ hồ và phát hiện duplicate/raw/unused;
  - Main <-> Pause synchronization hai chiều, không recursion, defaults/key persistence đúng.
- PlayMode/two-peer harness:
  - bash/hit/death của host và client, local và remote, vào đúng animation state đúng một lần;
  - damage/death authority không bị chuyển sang proxy;
  - không có visible jitter trong idle, walk, run, stop, đổi hướng, aim/crouch, collision, respawn và vehicle; ghi lại threshold đo được;
  - Main Menu/Pause Menu đổi cả ba slider hai chiều, preview/save/reopen/scene transition đúng; Music/SFX độc lập và master tác động cả hai.
- Kiểm tra Unity Console, compile status, test result XML/log và chụp screenshot/video cho hành vi multiplayer/UI cần chứng minh.
- Trả về chính xác file đã đổi và mọi artifact test/diagnostic đã tạo. Không commit/push/merge nếu chưa được yêu cầu riêng.
