const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const OUT = path.join(__dirname, 'Fragments_of_Survival_UML_Professional_v2.drawio');
const pages = [];

const S = {
  boundary: 'swimlane;html=1;horizontal=1;startSize=38;rounded=0;fillColor=none;strokeColor=#1F2937;strokeWidth=2;fontStyle=1;fontSize=16;fontFamily=Arial;',
  actor: 'shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;fontSize=14;fontFamily=Arial;',
  usecase: 'ellipse;whiteSpace=wrap;html=1;strokeWidth=2;fillColor=#FFFFFF;strokeColor=#1F2937;fontSize=14;fontFamily=Arial;',
  usecaseSoft: 'ellipse;whiteSpace=wrap;html=1;strokeWidth=1;fillColor=#F8FAFC;strokeColor=#64748B;fontSize=13;fontFamily=Arial;',
  note: 'shape=note;whiteSpace=wrap;html=1;size=15;fillColor=#FFF7CC;strokeColor=#B79A33;fontSize=12;fontFamily=Arial;',
  activity: 'rounded=1;whiteSpace=wrap;html=1;arcSize=12;strokeWidth=2;fillColor=#FFFFFF;strokeColor=#1F2937;fontSize=14;fontFamily=Arial;',
  activitySoft: 'rounded=1;whiteSpace=wrap;html=1;arcSize=12;strokeWidth=1;fillColor=#F8FAFC;strokeColor=#64748B;fontSize=13;fontFamily=Arial;',
  decision: 'rhombus;whiteSpace=wrap;html=1;strokeWidth=2;fillColor=#FFF7CC;strokeColor=#8A6D1D;fontSize=13;fontFamily=Arial;',
  start: 'ellipse;html=1;aspect=fixed;fillColor=#1F2937;strokeColor=#1F2937;',
  end: 'ellipse;shape=doubleEllipse;html=1;aspect=fixed;fillColor=#1F2937;strokeColor=#1F2937;',
  title: 'text;html=1;align=center;verticalAlign=middle;fontStyle=1;fontSize=22;fontFamily=Arial;',
  lane: 'swimlane;html=1;horizontal=0;startSize=34;rounded=0;fillColor=none;strokeColor=#64748B;fontStyle=1;fontSize=14;fontFamily=Arial;'
};

function esc(value) {
  return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function makePage(name, width = 1800, height = 1100) {
  const p = { name, width, height, cells: [], next: 2 };
  pages.push(p);
  return p;
}

function vertex(p, label, x, y, w, h, style, id) {
  const cellId = id || String(p.next++);
  p.cells.push(`<mxCell id="${cellId}" value="${esc(label)}" style="${style}" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/></mxCell>`);
  return cellId;
}

function edge(p, source, target, label = '', type = 'assoc', extra = '') {
  const id = String(p.next++);
  const styles = {
    assoc: 'edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeWidth=2;',
    include: 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;dashed=1;dashPattern=6 4;endArrow=open;endFill=0;strokeWidth=1.5;fontSize=12;labelBackgroundColor=#FFFFFF;',
    extend: 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;dashed=1;dashPattern=3 3;endArrow=open;endFill=0;strokeWidth=1.5;fontSize=12;labelBackgroundColor=#FFFFFF;',
    general: 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;endArrow=block;endFill=0;strokeWidth=1.5;',
    flow: 'edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeWidth=2;fontSize=12;labelBackgroundColor=#FFFFFF;',
    note: 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;dashed=1;endArrow=none;strokeWidth=1;'
  };
  p.cells.push(`<mxCell id="${id}" value="${esc(label)}" style="${styles[type]}${extra}" edge="1" parent="1" source="${source}" target="${target}"><mxGeometry relative="1" as="geometry"/></mxCell>`);
  return id;
}

function boundary(p, title, x = 210, y = 45, w = 1540, h = 980) { return vertex(p, title, x, y, w, h, S.boundary); }
function actor(p, label, x, y) { return vertex(p, label, x, y, 85, 120, S.actor); }
function uc(p, label, x, y, w = 225, h = 62, soft = false) { return vertex(p, label, x, y, w, h, soft ? S.usecaseSoft : S.usecase); }
function note(p, label, x, y, w = 240, h = 95) { return vertex(p, label, x, y, w, h, S.note); }
function activity(p, label, x, y, w = 250, h = 62, soft = false) { return vertex(p, label, x, y, w, h, soft ? S.activitySoft : S.activity); }
function decision(p, label, x, y, w = 180, h = 100) { return vertex(p, label, x, y, w, h, S.decision); }

function overviewPage() {
  const p = makePage('01 - Tổng quan hệ thống', 2000, 1200);
  boundary(p, 'FRAGMENTS OF SURVIVAL — USE CASE TỔNG QUAN', 230, 45, 1720, 1080);
  const player = actor(p, 'Người chơi', 65, 420);
  const host = actor(p, 'Chủ phòng', 65, 700);
  const member = actor(p, 'Thành viên phòng', 65, 900);
  edge(p, host, player, '', 'general');
  edge(p, member, player, '', 'general');

  const menu = uc(p, 'Sử dụng Main Menu', 320, 100, 260);
  const solo = uc(p, 'Khởi tạo chơi đơn', 320, 230, 250);
  const tutorial = uc(p, 'Chơi hướng dẫn', 320, 360, 250);
  const multi = uc(p, 'Khởi tạo Multiplayer', 320, 490, 260);
  const options = uc(p, 'Thiết lập trò chơi', 320, 620, 250);
  const credits = uc(p, 'Xem Credits', 320, 750, 230);
  const quit = uc(p, 'Thoát trò chơi', 320, 880, 230);
  [menu, solo, tutorial, multi, options, credits, quit].forEach(v => edge(p, player, v));

  const play = uc(p, 'Tham gia Gameplay', 780, 100, 260);
  const explore = uc(p, 'Khám phá thế giới', 780, 230, 250);
  const survive = uc(p, 'Quản lý sinh tồn', 780, 360, 250);
  const inventory = uc(p, 'Quản lý vật phẩm', 780, 490, 250);
  const combat = uc(p, 'Chiến đấu zombie', 780, 620, 250);
  const health = uc(p, 'Điều trị thương tích', 780, 750, 250);
  const quest = uc(p, 'Hoàn thành Main Quest', 780, 880, 270);
  [play, explore, survive, inventory, combat, health, quest].forEach(v => edge(p, player, v));

  const cooperate = uc(p, 'Phối hợp đồng đội', 1250, 180, 260);
  const trade = uc(p, 'Trao đổi vật phẩm', 1250, 310, 250);
  const chat = uc(p, 'Chat / Voice Chat', 1250, 440, 250);
  const wait = uc(p, 'Sử dụng phòng chờ', 1250, 570, 260);
  const pause = uc(p, 'Tạm dừng và tiếp tục', 1250, 700, 270);
  [cooperate, trade, chat, wait, pause].forEach(v => edge(p, player, v));
  edge(p, host, wait);
  edge(p, member, wait);
  edge(p, pause, play, '<<extend>>', 'extend');

  const escape = uc(p, 'Sửa xe và thoát khỏi khu vực', 1580, 830, 290, 68);
  const ending = uc(p, 'Xem kết thúc Main Play', 1580, 960, 290, 68);
  edge(p, quest, escape, '<<include>>', 'include');
  edge(p, escape, ending, '<<include>>', 'include');
  const legend1 = note(p, 'NÉT LIỀN\nActor trực tiếp sử dụng một chức năng. Các năng lực gameplay độc lập không bị ép thành include/extend.', 1550, 100, 330, 125);
  const legend2 = note(p, '<<include>> — NÉT ĐỨT\nA luôn phải gọi B. Mũi tên đi từ A tới B.', 1550, 270, 330, 105);
  const legend3 = note(p, '<<extend>> — NÉT ĐỨT\nChức năng mở rộng chỉ xảy ra theo lựa chọn/điều kiện. Mũi tên chỉ về use case gốc.', 1550, 420, 330, 125);
  const legend4 = note(p, 'TAM GIÁC RỖNG\nActor chuyên biệt chỉ về actor tổng quát: Chủ phòng / Thành viên phòng → Người chơi.', 1550, 590, 330, 125);
  edge(p, legend1, play, '', 'note');
  edge(p, legend2, escape, '', 'note');
  edge(p, legend3, pause, '', 'note');
  edge(p, legend4, host, '', 'note');
}

function menuPage() {
  const p = makePage('02 - Main Menu chi tiết');
  boundary(p, 'USE CASE — MAIN MENU VÀ ĐIỀU HƯỚNG');
  const player = actor(p, 'Người chơi', 70, 440);
  const menu = uc(p, 'Mở Main Menu', 300, 120, 240);
  edge(p, player, menu);
  const solo = uc(p, 'Chọn SOLO', 650, 100);
  const tutorial = uc(p, 'Chọn HƯỚNG DẪN', 650, 205);
  const multi = uc(p, 'Chọn MULTIPLAYER', 650, 310);
  const options = uc(p, 'Chọn OPTIONS', 650, 415);
  const credits = uc(p, 'Chọn CREDITS', 650, 520);
  const quit = uc(p, 'Chọn QUIT', 650, 625);
  [solo, tutorial, multi, options, credits, quit].forEach(v => edge(p, player, v));
  const diff = uc(p, 'Mở màn hình chọn độ khó', 1030, 80, 270, 58, true);
  const intro = uc(p, 'Tải scene Tutorial độc lập', 1030, 190, 270, 58, true);
  const lobby = uc(p, 'Mở Host / Join Game', 1030, 300, 270, 58, true);
  const settingTabs = uc(p, 'Mở Display / Controls / Audio', 1030, 410, 290, 58, true);
  const team = uc(p, 'Xem thông tin nhóm phát triển', 1030, 520, 290, 58, true);
  const close = uc(p, 'Đóng ứng dụng', 1030, 630, 270, 58, true);
  edge(p, solo, diff, '<<include>>', 'include');
  edge(p, tutorial, intro, '<<include>>', 'include');
  edge(p, multi, lobby, '<<include>>', 'include');
  edge(p, options, settingTabs, '<<include>>', 'include');
  edge(p, credits, team, '<<include>>', 'include');
  edge(p, quit, close, '<<include>>', 'include');
  const back = uc(p, 'Quay lại Main Menu', 1030, 760, 270, 58);
  [diff, lobby, settingTabs, team].forEach(v => edge(p, v, back, '<<extend>>', 'extend'));
  const n = note(p, 'Tutorial đi thẳng vào scene riêng ở độ khó Easy; không chọn nhân vật.', 1370, 160, 300, 110);
  edge(p, n, intro, '', 'note');
}

function soloPage() {
  const p = makePage('03 - Chơi đơn');
  boundary(p, 'USE CASE — KHỞI TẠO CHƠI ĐƠN');
  const player = actor(p, 'Người chơi', 70, 440);
  const solo = uc(p, 'Bắt đầu chơi đơn', 300, 120, 250);
  edge(p, player, solo);
  const diff = uc(p, 'Chọn độ khó', 650, 100);
  const easy = uc(p, 'Easy', 1010, 60, 190, 55, true);
  const medium = uc(p, 'Medium', 1010, 140, 190, 55, true);
  const hard = uc(p, 'Hard', 1010, 220, 190, 55, true);
  edge(p, solo, diff, '<<include>>', 'include');
  edge(p, easy, diff, '<<extend>>', 'extend');
  edge(p, medium, diff, '<<extend>>', 'extend');
  edge(p, hard, diff, '<<extend>>', 'extend');
  const char = uc(p, 'Chọn Survivor', 650, 360);
  const c1 = uc(p, 'Survivor: Unknown', 1010, 330, 230, 55, true);
  const c2 = uc(p, 'Survivor: Phantom', 1010, 420, 230, 55, true);
  edge(p, solo, char, '<<include>>', 'include');
  edge(p, c1, char, '<<extend>>', 'extend');
  edge(p, c2, char, '<<extend>>', 'extend');
  const skill = uc(p, 'Xem kỹ năng nhân vật', 1320, 375, 250, 58, true);
  edge(p, char, skill, '<<include>>', 'include');
  const name = uc(p, 'Nhập tên người chơi', 650, 540);
  const confirm = uc(p, 'Xác nhận lựa chọn', 650, 650);
  const load = uc(p, 'Hiển thị Loading', 1010, 650, 230, 58);
  const main = uc(p, 'Tải Main Play', 1320, 650, 230, 58);
  edge(p, solo, name, '<<include>>', 'include');
  edge(p, solo, confirm, '<<include>>', 'include');
  edge(p, confirm, load, '<<include>>', 'include');
  edge(p, load, main, '<<include>>', 'include');
  const back = uc(p, 'Quay lại chọn độ khó', 650, 790, 250, 58, true);
  edge(p, back, char, '<<extend>>', 'extend');
  edge(p, player, back);
}

function multiplayerPage() {
  const p = makePage('04 - Multiplayer Host và Client', 1900, 1200);
  boundary(p, 'USE CASE — MULTIPLAYER: HOST, CLIENT VÀ PHÒNG CHỜ', 230, 45, 1620, 1080);
  const host = actor(p, 'Chủ phòng', 55, 300);
  const client = actor(p, 'Thành viên phòng', 55, 760);
  const open = uc(p, 'Mở Multiplayer', 300, 100, 250);
  edge(p, host, open); edge(p, client, open);

  const create = uc(p, 'HOST GAME / Tạo phòng', 300, 230, 260);
  edge(p, host, create);
  const roomName = uc(p, 'Nhập tên phòng', 650, 120, 220, 55, true);
  const slots = uc(p, 'Chọn số người tối đa', 650, 200, 220, 55, true);
  const diff = uc(p, 'Chọn độ khó', 650, 280, 220, 55, true);
  const passToggle = uc(p, 'Bật mật khẩu phòng', 650, 360, 220, 55, true);
  const pass = uc(p, 'Nhập mật khẩu', 980, 360, 220, 55, true);
  [roomName, slots, diff].forEach(v => edge(p, create, v, '<<include>>', 'include'));
  edge(p, passToggle, create, '<<extend>>', 'extend');
  edge(p, pass, passToggle, '<<include>>', 'include');

  const join = uc(p, 'JOIN GAME / Tham gia phòng', 300, 690, 280);
  edge(p, client, join);
  const lobby = uc(p, 'Kết nối Lobby', 650, 580, 220, 55, true);
  const list = uc(p, 'Xem / làm mới danh sách phòng', 650, 660, 270, 55, true);
  const chooseRoom = uc(p, 'Chọn phòng', 650, 740, 220, 55, true);
  const joinPass = uc(p, 'Nhập mật khẩu phòng', 650, 820, 240, 55, true);
  edge(p, join, lobby, '<<include>>', 'include');
  edge(p, join, list, '<<include>>', 'include');
  edge(p, join, chooseRoom, '<<include>>', 'include');
  edge(p, joinPass, chooseRoom, '<<extend>>', 'extend');

  const select = uc(p, 'Chọn nhân vật', 1040, 580, 220);
  const name = uc(p, 'Nhập tên người chơi', 1040, 680, 230);
  const connect = uc(p, 'Kết nối phiên chơi', 1040, 780, 230);
  edge(p, create, select, '<<include>>', 'include');
  edge(p, join, select, '<<include>>', 'include');
  edge(p, select, name, '<<include>>', 'include');
  edge(p, name, connect, '<<include>>', 'include');

  const waiting = uc(p, 'Vào phòng chờ', 1370, 530, 230);
  const roster = uc(p, 'Xem danh sách người chơi', 1370, 630, 250, 55, true);
  const start = uc(p, 'START CAMPAIGN', 1370, 730, 230);
  const waitHost = uc(p, 'Chờ Host bắt đầu', 1370, 830, 230, 55, true);
  edge(p, connect, waiting, '<<include>>', 'include');
  edge(p, waiting, roster, '<<include>>', 'include');
  edge(p, host, start);
  edge(p, client, waitHost);
  edge(p, start, waiting, '<<extend>>', 'extend');
  edge(p, waitHost, waiting, '<<extend>>', 'extend');

  const loading = uc(p, 'Đồng bộ Loading', 1660, 650, 190);
  const gameplay = uc(p, 'Vào Main Play', 1660, 770, 190);
  edge(p, start, loading, '<<include>>', 'include');
  edge(p, waitHost, loading, '<<include>>', 'include');
  edge(p, loading, gameplay, '<<include>>', 'include');
  const quit = uc(p, 'Rời phòng chờ', 1370, 960, 230, 55, true);
  edge(p, host, quit); edge(p, client, quit);
}

function multiplayerHostPage() {
  const p = makePage('04 - Multiplayer Host', 1900, 1200);
  boundary(p, 'USE CASE — MULTIPLAYER: CHỦ PHÒNG (HOST)', 230, 45, 1620, 1080);
  const host = actor(p, 'Chủ phòng', 65, 500);
  const hostGame = uc(p, 'Tạo phòng Multiplayer', 320, 100, 280);
  edge(p, host, hostGame);
  const roomName = uc(p, 'Nhập tên phòng', 720, 70, 240, 58, true);
  const maxPlayers = uc(p, 'Chọn số người tối đa', 720, 160, 250, 58, true);
  const difficulty = uc(p, 'Chọn độ khó', 720, 250, 230, 58, true);
  [roomName, maxPlayers, difficulty].forEach(v => edge(p, hostGame, v, '<<include>>', 'include'));
  const enablePassword = uc(p, 'Bật bảo vệ bằng mật khẩu', 720, 360, 280, 58, true);
  const enterPassword = uc(p, 'Nhập mật khẩu phòng', 1110, 360, 250, 58, true);
  edge(p, enablePassword, hostGame, '<<extend>>', 'extend');
  edge(p, enablePassword, enterPassword, '<<include>>', 'include');
  const selectCharacter = uc(p, 'Chọn nhân vật', 320, 510, 250);
  const viewSkill = uc(p, 'Xem kỹ năng nhân vật', 720, 470, 260, 58, true);
  const enterName = uc(p, 'Nhập tên người chơi', 720, 560, 250, 58, true);
  edge(p, host, selectCharacter);
  edge(p, selectCharacter, viewSkill, '<<include>>', 'include');
  edge(p, selectCharacter, enterName, '<<include>>', 'include');
  const connect = uc(p, 'Khởi tạo phiên Host', 320, 720, 260);
  const waiting = uc(p, 'Vào phòng chờ', 720, 700, 240);
  const roster = uc(p, 'Xem danh sách người chơi', 1110, 660, 270, 58, true);
  const startCampaign = uc(p, 'Bắt đầu Campaign', 1110, 750, 250);
  edge(p, host, connect);
  edge(p, connect, waiting, '<<include>>', 'include');
  edge(p, waiting, roster, '<<include>>', 'include');
  edge(p, host, startCampaign);
  const loading = uc(p, 'Đồng bộ và hiển thị Loading', 1480, 700, 290, 58, true);
  const gameplay = uc(p, 'Tải Main Play', 1480, 810, 250);
  edge(p, startCampaign, loading, '<<include>>', 'include');
  edge(p, startCampaign, gameplay, '<<include>>', 'include');
  const leave = uc(p, 'Rời phòng chờ', 720, 900, 240, 58);
  edge(p, host, leave);
  const n1 = note(p, 'Chọn độ khó là bước bắt buộc của Tạo phòng, không phải bước bắt buộc của Client tham gia phòng.', 1050, 90, 360, 120);
  const n2 = note(p, 'Đặt mật khẩu là tùy chọn nên dùng <<extend>>. Nếu bật, nhập mật khẩu trở thành bước bắt buộc của nhánh này.', 1450, 300, 340, 130);
  edge(p, n1, difficulty, '', 'note');
  edge(p, n2, enablePassword, '', 'note');
}

function multiplayerClientPage() {
  const p = makePage('05 - Multiplayer Client', 1900, 1200);
  boundary(p, 'USE CASE — MULTIPLAYER: THÀNH VIÊN PHÒNG (CLIENT)', 230, 45, 1620, 1080);
  const client = actor(p, 'Thành viên phòng', 65, 500);
  const joinGame = uc(p, 'Tham gia phòng Multiplayer', 320, 110, 300);
  edge(p, client, joinGame);
  const lobby = uc(p, 'Kết nối Lobby', 720, 70, 230, 58, true);
  const list = uc(p, 'Xem danh sách phòng', 720, 160, 250, 58, true);
  const refresh = uc(p, 'Làm mới danh sách phòng', 1110, 160, 270, 58, true);
  const selectRoom = uc(p, 'Chọn phòng', 720, 250, 220, 58, true);
  edge(p, joinGame, lobby, '<<include>>', 'include');
  edge(p, joinGame, list, '<<include>>', 'include');
  edge(p, refresh, list, '<<extend>>', 'extend');
  edge(p, joinGame, selectRoom, '<<include>>', 'include');
  const password = uc(p, 'Nhập mật khẩu phòng', 1110, 270, 260, 58, true);
  edge(p, password, selectRoom, '<<extend>>', 'extend');
  const character = uc(p, 'Chọn nhân vật', 320, 500, 250);
  const skill = uc(p, 'Xem kỹ năng nhân vật', 720, 460, 260, 58, true);
  const name = uc(p, 'Nhập tên người chơi', 720, 550, 250, 58, true);
  edge(p, client, character);
  edge(p, character, skill, '<<include>>', 'include');
  edge(p, character, name, '<<include>>', 'include');
  const connect = uc(p, 'Kết nối vào phiên chơi', 320, 710, 270);
  const waiting = uc(p, 'Vào phòng chờ', 720, 690, 240);
  const roster = uc(p, 'Xem danh sách người chơi', 1110, 650, 270, 58, true);
  const waitHost = uc(p, 'Chờ Host bắt đầu', 1110, 750, 250);
  edge(p, client, connect);
  edge(p, connect, waiting, '<<include>>', 'include');
  edge(p, waiting, roster, '<<include>>', 'include');
  edge(p, client, waitHost);
  const loading = uc(p, 'Nhận tín hiệu và Loading', 1480, 700, 270, 58, true);
  const gameplay = uc(p, 'Tải Main Play', 1480, 810, 250);
  edge(p, waitHost, loading, '<<include>>', 'include');
  edge(p, waitHost, gameplay, '<<include>>', 'include');
  const leave = uc(p, 'Rời phòng chờ', 720, 900, 240, 58);
  edge(p, client, leave);
  const n1 = note(p, 'Client không chọn độ khó. Client sử dụng cấu hình do Host đặt cho phòng.', 1110, 60, 320, 100);
  const n2 = note(p, 'Nhập mật khẩu chỉ mở rộng khi phòng được Host bảo vệ, nên mũi tên <<extend>> chỉ về Chọn phòng.', 1450, 250, 330, 125);
  edge(p, n1, joinGame, '', 'note');
  edge(p, n2, password, '', 'note');
}

function tutorialOpeningPage() {
  const p = makePage('06 - Tutorial mở đầu', 1900, 1150);
  boundary(p, 'USE CASE — TUTORIAL: CINEMATIC MỞ ĐẦU', 230, 45, 1620, 1020);
  const player = actor(p, 'Người chơi', 65, 480);
  const tutorial = uc(p, 'Bắt đầu Hướng dẫn', 320, 100, 270);
  edge(p, player, tutorial);
  const cinematic = uc(p, 'Xem cinematic mở đầu', 720, 100, 280);
  edge(p, tutorial, cinematic, '<<include>>', 'include');
  const eyes = uc(p, 'Mở mắt trong xe', 1120, 60, 240, 58, true);
  const radio = uc(p, 'Nghe thông báo chính phủ', 1120, 150, 280, 58, true);
  const drive = uc(p, 'Theo dõi xe chạy trên đường', 1120, 240, 290, 58, true);
  const exitShot = uc(p, 'Xem cảnh xe rời đoạn đường lặp', 1120, 330, 310, 58, true);
  [eyes, radio, drive, exitShot].forEach(v => edge(p, cinematic, v, '<<include>>', 'include'));
  const transition = uc(p, 'Chuyển cảnh màn hình đen', 720, 470, 280, 58, true);
  const trouble = uc(p, 'Xe chạy trên đoạn đường sự cố', 1120, 450, 300, 58, true);
  const breakdown = uc(p, 'Xe chết máy', 1120, 540, 220, 58, true);
  edge(p, cinematic, transition, '<<include>>', 'include');
  edge(p, cinematic, trouble, '<<include>>', 'include');
  edge(p, cinematic, breakdown, '<<include>>', 'include');
  const dialogue = uc(p, 'Đọc hội thoại', 720, 680, 240);
  const nextLine = uc(p, 'Nhấn E xem câu tiếp theo', 1120, 650, 280, 58, true);
  const leaveCar = uc(p, 'Nhấn E rời xe', 1120, 740, 240, 58, true);
  edge(p, player, dialogue);
  edge(p, dialogue, nextLine, '<<include>>', 'include');
  edge(p, dialogue, leaveCar, '<<include>>', 'include');
  const spawn = uc(p, 'Spawn nhân vật Tutorial', 1480, 700, 270);
  edge(p, leaveCar, spawn, '<<include>>', 'include');
  const n = note(p, 'Audio và camera là thành phần bắt buộc của cinematic mở đầu, không phải actor và không phải nhánh tùy chọn.', 1450, 130, 340, 130);
  edge(p, n, cinematic, '', 'note');
}

function tutorialTrainingPage() {
  const p = makePage('07 - Tutorial huấn luyện', 2100, 1300);
  boundary(p, 'USE CASE — TUTORIAL: CHUỖI HUẤN LUYỆN GAMEPLAY', 230, 45, 1820, 1160);
  const player = actor(p, 'Người chơi', 65, 520);
  const controls = uc(p, 'Học điều khiển', 320, 100, 250);
  const survival = uc(p, 'Học sinh tồn và loot', 320, 460, 270);
  const combat = uc(p, 'Học chiến đấu và chữa trị', 320, 850, 300);
  [controls, survival, combat].forEach(v => edge(p, player, v));
  const move = uc(p, 'Di chuyển WASD', 720, 60, 220, 58, true);
  const zoom = uc(p, 'Zoom camera', 720, 145, 220, 58, true);
  const observe = uc(p, 'Quan sát bằng chuột phải', 720, 230, 260, 58, true);
  [move, zoom, observe].forEach(v => edge(p, controls, v, '<<include>>', 'include'));
  const needs = uc(p, 'Theo dõi đói và khát', 720, 390, 250, 58, true);
  const house = uc(p, 'Đi đến căn nhà đánh dấu', 720, 475, 270, 58, true);
  const cabinet = uc(p, 'Mở tủ bếp', 720, 560, 220, 58, true);
  const loot = uc(p, 'Lấy toàn bộ vật phẩm', 1080, 390, 250, 58, true);
  const consume = uc(p, 'Ăn thịt và uống nước', 1080, 475, 250, 58, true);
  const hotbar = uc(p, 'Đưa S12K vào Hotbar', 1080, 560, 250, 58, true);
  const reload = uc(p, 'Nạp đạn S12K', 1440, 475, 220, 58, true);
  [needs, house, cabinet, loot, consume, hotbar, reload].forEach(v => edge(p, survival, v, '<<include>>', 'include'));
  const zombieA = uc(p, 'Camera giới thiệu Zombie A', 720, 760, 280, 58, true);
  const noise = uc(p, 'Theo dõi thanh độ ồn', 720, 845, 250, 58, true);
  const sneak = uc(p, 'Ngồi và lén tiếp cận', 720, 930, 250, 58, true);
  const melee = uc(p, 'Hạ Zombie A bằng cận chiến', 1080, 760, 290, 58, true);
  const wound = uc(p, 'Kiểm tra vết thương', 1080, 845, 240, 58, true);
  const bandage = uc(p, 'Băng bó', 1080, 930, 210, 58, true);
  const zombieB = uc(p, 'Camera giới thiệu Zombie B', 1440, 760, 280, 58, true);
  const shoot = uc(p, 'Bắn hạ Zombie B', 1440, 845, 240, 58, true);
  const horde = uc(p, 'Đối mặt đàn zombie', 1440, 930, 250, 58, true);
  [zombieA, noise, sneak, melee, wound, bandage, zombieB, shoot, horde].forEach(v => edge(p, combat, v, '<<include>>', 'include'));
  const ending = uc(p, 'Xem kết thúc Tutorial', 1770, 760, 270);
  const main = uc(p, 'Bắt đầu Main Play', 1770, 860, 250, 58, true);
  const replay = uc(p, 'Chơi lại Tutorial', 1770, 945, 250, 58, true);
  const menu = uc(p, 'Trở về Main Menu', 1770, 1030, 250, 58, true);
  edge(p, combat, ending, '<<include>>', 'include');
  [main, replay, menu].forEach(v => edge(p, v, ending, '<<extend>>', 'extend'));
}

function tutorialUseCasePage() {
  const p = makePage('05 - Hướng dẫn độc lập', 2000, 1250);
  boundary(p, 'USE CASE — SCENE HƯỚNG DẪN ĐỘC LẬP', 220, 45, 1730, 1120);
  const player = actor(p, 'Người chơi', 55, 520);
  const start = uc(p, 'Bắt đầu Hướng dẫn', 300, 90, 250);
  edge(p, player, start);
  const intro = uc(p, 'Xem cinematic mở đầu', 620, 90, 260);
  const openEyes = uc(p, 'Mở mắt trong xe', 980, 55, 220, 55, true);
  const radio = uc(p, 'Nghe thông báo chính phủ', 980, 130, 250, 55, true);
  const drive = uc(p, 'Theo dõi xe chạy qua thành phố', 1280, 55, 280, 55, true);
  const breakdown = uc(p, 'Xe gặp sự cố', 1280, 130, 220, 55, true);
  const dialogue = uc(p, 'Đọc hội thoại và rời xe', 1600, 90, 250, 58, true);
  edge(p, start, intro, '<<include>>', 'include');
  [openEyes, radio, drive, breakdown, dialogue].forEach(v => edge(p, intro, v, '<<include>>', 'include'));

  const controls = uc(p, 'Học điều khiển', 300, 280, 240);
  const move = uc(p, 'Di chuyển WASD', 620, 240, 220, 55, true);
  const zoom = uc(p, 'Zoom camera', 620, 320, 220, 55, true);
  const look = uc(p, 'Quan sát bằng chuột', 620, 400, 220, 55, true);
  edge(p, player, controls);
  [move, zoom, look].forEach(v => edge(p, controls, v, '<<include>>', 'include'));

  const survive = uc(p, 'Học sinh tồn và loot', 300, 530, 250);
  const needs = uc(p, 'Theo dõi đói và khát', 620, 500, 230, 55, true);
  const house = uc(p, 'Đi đến căn nhà đánh dấu', 620, 580, 250, 55, true);
  const cabinet = uc(p, 'Mở tủ và lấy vật phẩm', 620, 660, 250, 55, true);
  const consume = uc(p, 'Ăn thịt và uống nước', 940, 500, 240, 55, true);
  const hotbar = uc(p, 'Đưa S12K vào Hotbar', 940, 580, 240, 55, true);
  const reload = uc(p, 'Nạp đạn', 940, 660, 210, 55, true);
  edge(p, player, survive);
  [needs, house, cabinet, consume, hotbar, reload].forEach(v => edge(p, survive, v, '<<include>>', 'include'));

  const combat = uc(p, 'Học chiến đấu', 300, 820, 230);
  const revealA = uc(p, 'Camera giới thiệu Zombie A', 620, 780, 260, 55, true);
  const noise = uc(p, 'Theo dõi độ ồn', 620, 860, 220, 55, true);
  const sneak = uc(p, 'Ngồi và tiếp cận âm thầm', 620, 940, 260, 55, true);
  const melee = uc(p, 'Hạ Zombie A bằng cận chiến', 940, 780, 270, 55, true);
  const wound = uc(p, 'Kiểm tra vết thương', 940, 860, 230, 55, true);
  const bandage = uc(p, 'Băng bó', 940, 940, 210, 55, true);
  const revealB = uc(p, 'Camera giới thiệu Zombie B', 1280, 780, 260, 55, true);
  const shoot = uc(p, 'Bắn hạ Zombie B', 1280, 860, 230, 55, true);
  const horde = uc(p, 'Đối mặt đàn zombie', 1280, 940, 230, 55, true);
  edge(p, player, combat);
  [revealA, noise, sneak, melee, wound, bandage, revealB, shoot, horde].forEach(v => edge(p, combat, v, '<<include>>', 'include'));

  const ending = uc(p, 'Xem kết thúc Tutorial', 1600, 760, 260);
  const main = uc(p, 'Bắt đầu sinh tồn', 1600, 860, 230, 55, true);
  const replay = uc(p, 'Chơi lại hướng dẫn', 1600, 940, 230, 55, true);
  const menu = uc(p, 'Trở về Main Menu', 1600, 1020, 230, 55, true);
  edge(p, combat, ending, '<<include>>', 'include');
  [main, replay, menu].forEach(v => edge(p, v, ending, '<<extend>>', 'extend'));
}

function explorationPage() {
  const p = makePage('08 - Khám phá và thế giới', 2000, 1250);
  boundary(p, 'USE CASE — KHÁM PHÁ, DI CHUYỂN VÀ THẾ GIỚI');
  const player = actor(p, 'Người chơi', 65, 450);
  const explore = uc(p, 'Khám phá thế giới', 300, 120, 250);
  edge(p, player, explore);
  const move = uc(p, 'Di chuyển', 650, 80);
  const walk = uc(p, 'Đi bộ', 990, 45, 190, 55, true);
  const run = uc(p, 'Chạy nước rút', 990, 120, 190, 55, true);
  const crouch = uc(p, 'Ngồi / lén lút', 990, 195, 190, 55, true);
  edge(p, explore, move, '<<include>>', 'include');
  [walk, run, crouch].forEach(v => edge(p, v, move, '<<extend>>', 'extend'));
  const camera = uc(p, 'Điều khiển tầm nhìn', 650, 300);
  const aim = uc(p, 'Quan sát theo con trỏ', 990, 270, 230, 55, true);
  const zoom = uc(p, 'Zoom in / out', 990, 350, 210, 55, true);
  edge(p, explore, camera, '<<include>>', 'include');
  edge(p, camera, aim, '<<include>>', 'include');
  edge(p, camera, zoom, '<<include>>', 'include');
  const building = uc(p, 'Khám phá công trình', 650, 500);
  const enter = uc(p, 'Đi vào / ra khỏi nhà', 990, 460, 230, 55, true);
  const roof = uc(p, 'Ẩn mái và quan sát nội thất', 990, 540, 260, 55, true);
  edge(p, explore, building, '<<include>>', 'include');
  edge(p, building, enter, '<<include>>', 'include');
  edge(p, building, roof, '<<include>>', 'include');
  const world = uc(p, 'Tương tác môi trường', 650, 700);
  const map = uc(p, 'Mở bản đồ đã mở khóa', 990, 650, 250, 55, true);
  const fog = uc(p, 'Khám phá Fog of War', 990, 730, 230, 55, true);
  const cycle = uc(p, 'Thích nghi chu kỳ ngày / đêm', 990, 810, 280, 55, true);
  const sleep = uc(p, 'Ngủ trên giường', 1320, 810, 220, 55, true);
  edge(p, explore, world, '<<include>>', 'include');
  [map, fog, cycle].forEach(v => edge(p, world, v, '<<include>>', 'include'));
  edge(p, sleep, cycle, '<<extend>>', 'extend');
  const noise = uc(p, 'Tạo tiếng động', 1320, 300, 220);
  const attract = uc(p, 'Thu hút zombie', 1320, 430, 220, 55, true);
  edge(p, run, noise, '<<extend>>', 'extend');
  edge(p, noise, attract, '<<include>>', 'include');
}

function survivalHealthPage() {
  const p = makePage('07 - Sinh tồn và sức khỏe', 1900, 1200);
  boundary(p, 'USE CASE — SINH TỒN, THỂ TRẠNG VÀ SỨC KHỎE', 220, 45, 1630, 1080);
  const player = actor(p, 'Người chơi', 60, 500);
  const survival = uc(p, 'Quản lý sinh tồn', 300, 110, 250);
  const health = uc(p, 'Quản lý sức khỏe', 300, 620, 250);
  edge(p, player, survival); edge(p, player, health);
  const hunger = uc(p, 'Theo dõi đói', 650, 70, 210, 55, true);
  const thirst = uc(p, 'Theo dõi khát', 650, 150, 210, 55, true);
  const stamina = uc(p, 'Theo dõi stamina', 650, 230, 220, 55, true);
  const fatigue = uc(p, 'Theo dõi mệt mỏi', 650, 310, 220, 55, true);
  [hunger, thirst, stamina, fatigue].forEach(v => edge(p, survival, v, '<<include>>', 'include'));
  const eat = uc(p, 'Ăn thức ăn', 1010, 70, 210, 55, true);
  const drink = uc(p, 'Uống nước', 1010, 150, 210, 55, true);
  const nutrition = uc(p, 'Nhận hiệu ứng dinh dưỡng', 1010, 230, 250, 55, true);
  const rest = uc(p, 'Ngủ / nghỉ ngơi', 1010, 310, 220, 55, true);
  edge(p, eat, hunger, '<<extend>>', 'extend');
  edge(p, drink, thirst, '<<extend>>', 'extend');
  edge(p, nutrition, eat, '<<extend>>', 'extend');
  edge(p, rest, fatigue, '<<extend>>', 'extend');

  const body = uc(p, 'Mở Health Status', 650, 520, 230);
  const injury = uc(p, 'Kiểm tra bộ phận bị thương', 650, 610, 270, 55, true);
  const bleeding = uc(p, 'Theo dõi chảy máu', 650, 690, 230, 55, true);
  const bandage = uc(p, 'Băng bó vết thương', 1010, 520, 230, 55, true);
  const remove = uc(p, 'Tháo / thay băng', 1010, 600, 220, 55, true);
  const painkiller = uc(p, 'Dùng thuốc giảm đau', 1010, 680, 230, 55, true);
  const heal = uc(p, 'Hồi phục máu', 1010, 760, 220, 55, true);
  edge(p, health, body, '<<include>>', 'include');
  edge(p, body, injury, '<<include>>', 'include');
  edge(p, body, bleeding, '<<include>>', 'include');
  edge(p, bandage, injury, '<<extend>>', 'extend');
  edge(p, remove, bandage, '<<extend>>', 'extend');
  edge(p, painkiller, body, '<<extend>>', 'extend');
  edge(p, heal, health, '<<extend>>', 'extend');
  const bitten = uc(p, 'Bị zombie cắn', 1370, 500, 220, 55, true);
  const infect = uc(p, 'Tiến triển nhiễm bệnh', 1370, 590, 240, 55, true);
  const die = uc(p, 'Nhân vật tử vong', 1370, 680, 220, 55, true);
  const spectate = uc(p, 'Theo dõi đồng đội', 1370, 770, 220, 55, true);
  const respawn = uc(p, 'Respawn', 1370, 860, 200, 55, true);
  edge(p, bitten, health, '<<extend>>', 'extend');
  edge(p, bitten, infect, '<<include>>', 'include');
  edge(p, infect, die, '<<include>>', 'include');
  edge(p, die, spectate, '<<include>>', 'include');
  edge(p, respawn, spectate, '<<extend>>', 'extend');
}

function inventoryCombatPage() {
  const p = makePage('08 - Vật phẩm và chiến đấu', 1900, 1200);
  boundary(p, 'USE CASE — LOOT, INVENTORY, HOTBAR VÀ CHIẾN ĐẤU', 220, 45, 1630, 1080);
  const player = actor(p, 'Người chơi', 60, 500);
  const inventory = uc(p, 'Quản lý vật phẩm', 300, 120, 250);
  const combat = uc(p, 'Chiến đấu', 300, 690, 230);
  edge(p, player, inventory); edge(p, player, combat);
  const search = uc(p, 'Mở Loot Container', 650, 70, 230, 55, true);
  const take = uc(p, 'Lấy vật phẩm', 650, 150, 210, 55, true);
  const store = uc(p, 'Cất vật phẩm vào tủ', 650, 230, 230, 55, true);
  const openInv = uc(p, 'Mở Inventory', 650, 310, 210, 55, true);
  const arrange = uc(p, 'Sắp xếp / đổi vị trí', 650, 390, 230, 55, true);
  const use = uc(p, 'Sử dụng vật phẩm', 1010, 70, 220, 55, true);
  const drop = uc(p, 'Thả vật phẩm', 1010, 150, 210, 55, true);
  const hotbar = uc(p, 'Đưa vật phẩm vào Hotbar', 1010, 230, 250, 55, true);
  const backpack = uc(p, 'Trang bị balo mở rộng', 1010, 310, 240, 55, true);
  [search, openInv].forEach(v => edge(p, inventory, v, '<<include>>', 'include'));
  [take, store].forEach(v => edge(p, v, search, '<<extend>>', 'extend'));
  [arrange, use, drop, hotbar, backpack].forEach(v => edge(p, v, openInv, '<<extend>>', 'extend'));

  const equip = uc(p, 'Trang bị vũ khí từ Hotbar', 650, 590, 270, 55, true);
  const aim = uc(p, 'Ngắm hướng tấn công', 650, 670, 230, 55, true);
  const melee = uc(p, 'Tấn công cận chiến', 650, 750, 230, 55, true);
  const shoot = uc(p, 'Bắn súng', 650, 830, 210, 55, true);
  const reload = uc(p, 'Nạp đạn', 650, 910, 210, 55, true);
  [equip, aim].forEach(v => edge(p, combat, v, '<<include>>', 'include'));
  edge(p, melee, combat, '<<extend>>', 'extend');
  edge(p, shoot, combat, '<<extend>>', 'extend');
  edge(p, reload, shoot, '<<extend>>', 'extend');
  const ammo = uc(p, 'Quản lý đạn dự trữ', 1010, 830, 230, 55, true);
  const hit = uc(p, 'Gây sát thương zombie', 1010, 670, 250, 55, true);
  const noise = uc(p, 'Tạo tiếng động chiến đấu', 1010, 750, 260, 55, true);
  edge(p, shoot, ammo, '<<include>>', 'include');
  edge(p, melee, hit, '<<include>>', 'include');
  edge(p, shoot, hit, '<<include>>', 'include');
  edge(p, melee, noise, '<<include>>', 'include');
  edge(p, shoot, noise, '<<include>>', 'include');
  const attract = uc(p, 'Zombie nghe và truy đuổi', 1370, 750, 250, 58, true);
  const kill = uc(p, 'Hạ zombie', 1370, 670, 210, 55, true);
  edge(p, noise, attract, '<<include>>', 'include');
  edge(p, hit, kill, '<<include>>', 'include');
}

function survivalPage() {
  const p = makePage('09 - Sinh tồn', 2000, 1250);
  boundary(p, 'USE CASE — CHỈ SỐ SINH TỒN VÀ NHU CẦU CƠ BẢN', 230, 45, 1720, 1120);
  const player = actor(p, 'Người chơi', 65, 500);
  const survival = uc(p, 'Quản lý sinh tồn', 330, 120, 270);
  edge(p, player, survival);

  const hunger = uc(p, 'Theo dõi chỉ số đói', 720, 90, 250);
  const thirst = uc(p, 'Theo dõi chỉ số khát', 720, 230, 250);
  const stamina = uc(p, 'Theo dõi Stamina', 720, 370, 250);
  const fatigue = uc(p, 'Theo dõi mệt mỏi', 720, 510, 250);
  [hunger, thirst, stamina, fatigue].forEach(v => edge(p, survival, v, '<<include>>', 'include'));

  const eat = uc(p, 'Ăn thức ăn', 1120, 70, 230, 58, true);
  const drink = uc(p, 'Uống nước', 1120, 210, 230, 58, true);
  const run = uc(p, 'Chạy nước rút', 1120, 350, 230, 58, true);
  const sleep = uc(p, 'Ngủ / nghỉ ngơi', 1120, 490, 230, 58, true);
  edge(p, eat, hunger, '<<extend>>', 'extend');
  edge(p, drink, thirst, '<<extend>>', 'extend');
  edge(p, run, stamina, '<<extend>>', 'extend');
  edge(p, sleep, fatigue, '<<extend>>', 'extend');

  const nutrition = uc(p, 'Áp dụng hiệu ứng dinh dưỡng', 1510, 70, 290, 58, true);
  const restore = uc(p, 'Phục hồi Stamina', 1510, 350, 250, 58, true);
  const time = uc(p, 'Chuyển nhanh thời gian', 1510, 490, 260, 58, true);
  edge(p, eat, nutrition, '<<include>>', 'include');
  edge(p, drink, nutrition, '<<include>>', 'include');
  edge(p, sleep, restore, '<<include>>', 'include');
  edge(p, sleep, time, '<<include>>', 'include');

  const warning = uc(p, 'Hiển thị cảnh báo chỉ số thấp', 720, 720, 300);
  const penalty = uc(p, 'Áp dụng bất lợi sinh tồn', 1120, 720, 280, 58, true);
  edge(p, survival, warning, '<<include>>', 'include');
  edge(p, warning, penalty, '<<include>>', 'include');
  const n1 = note(p, '<<include>>: hệ thống luôn cập nhật các chỉ số khi quản lý sinh tồn.', 330, 900, 340, 115);
  const n2 = note(p, '<<extend>>: ăn, uống, chạy hoặc ngủ chỉ xảy ra khi người chơi lựa chọn / đủ điều kiện.', 780, 900, 370, 125);
  edge(p, n1, survival, '', 'note');
  edge(p, n2, eat, '', 'note');
}

function healthPage() {
  const p = makePage('10 - Sức khỏe, nhiễm bệnh và tử vong', 2000, 1300);
  boundary(p, 'USE CASE — SỨC KHỎE, ĐIỀU TRỊ, NHIỄM BỆNH VÀ TỬ VONG', 230, 45, 1720, 1170);
  const player = actor(p, 'Người chơi', 65, 510);
  const health = uc(p, 'Quản lý sức khỏe', 330, 100, 270);
  const status = uc(p, 'Mở Health Status', 720, 80, 260);
  const parts = uc(p, 'Kiểm tra từng bộ phận cơ thể', 1120, 70, 300, 58, true);
  const bleeding = uc(p, 'Theo dõi chảy máu / HP', 1120, 180, 280, 58, true);
  edge(p, player, health);
  edge(p, health, status, '<<include>>', 'include');
  edge(p, status, parts, '<<include>>', 'include');
  edge(p, status, bleeding, '<<include>>', 'include');

  const bandage = uc(p, 'Băng bó vết thương', 720, 360, 260);
  const remove = uc(p, 'Tháo / thay băng', 1120, 330, 250, 58, true);
  const medicine = uc(p, 'Dùng thuốc giảm đau', 1120, 440, 260, 58, true);
  const heal = uc(p, 'Hồi phục theo thời gian', 1510, 385, 280, 58, true);
  edge(p, player, bandage);
  edge(p, bandage, parts, '<<include>>', 'include');
  edge(p, remove, bandage, '<<extend>>', 'extend');
  edge(p, medicine, status, '<<extend>>', 'extend');
  edge(p, heal, bandage, '<<extend>>', 'extend');

  const bitten = uc(p, 'Bị zombie cắn', 330, 650, 250);
  const infection = uc(p, 'Theo dõi mức nhiễm bệnh', 720, 630, 280);
  const symptoms = uc(p, 'Hiển thị triệu chứng', 1120, 600, 260, 58, true);
  const death = uc(p, 'Nhân vật tử vong', 1120, 730, 250, 58, true);
  edge(p, bitten, health, '<<extend>>', 'extend');
  edge(p, bitten, infection, '<<include>>', 'include');
  edge(p, infection, symptoms, '<<include>>', 'include');
  edge(p, death, infection, '<<extend>>', 'extend');

  const gameOver = uc(p, 'Hiện màn hình tử vong', 1510, 650, 280, 58, true);
  const spectate = uc(p, 'Theo dõi đồng đội', 1510, 770, 250, 58, true);
  const respawn = uc(p, 'Respawn vào phiên chơi', 1510, 890, 270, 58, true);
  edge(p, death, gameOver, '<<include>>', 'include');
  edge(p, spectate, gameOver, '<<extend>>', 'extend');
  edge(p, respawn, spectate, '<<extend>>', 'extend');
  const n = note(p, 'Tử vong là nhánh điều kiện nên «extend» tiến triển nhiễm bệnh; không phải mọi vết cắn đều được mô tả là chết ngay.', 690, 990, 420, 125);
  edge(p, n, death, '', 'note');
}

function inventoryPage() {
  const p = makePage('11 - Loot, Inventory và Hotbar', 2000, 1300);
  boundary(p, 'USE CASE — LOOT, INVENTORY, TRANG BỊ VÀ HOTBAR', 230, 45, 1720, 1170);
  const player = actor(p, 'Người chơi', 65, 510);
  const loot = uc(p, 'Tương tác Loot Container', 330, 100, 290);
  const showLoot = uc(p, 'Hiển thị vật phẩm trong tủ', 740, 80, 300, 58, true);
  const take = uc(p, 'Lấy vật phẩm', 1130, 55, 240, 58, true);
  const store = uc(p, 'Cất vật phẩm', 1130, 160, 240, 58, true);
  edge(p, player, loot);
  edge(p, loot, showLoot, '<<include>>', 'include');
  edge(p, take, showLoot, '<<extend>>', 'extend');
  edge(p, store, showLoot, '<<extend>>', 'extend');

  const inv = uc(p, 'Mở Inventory', 330, 390, 260);
  const grid = uc(p, 'Hiển thị ô và sức chứa', 740, 350, 280, 58, true);
  const move = uc(p, 'Sắp xếp / đổi vị trí', 1130, 310, 270, 58, true);
  const use = uc(p, 'Sử dụng vật phẩm', 1130, 415, 250, 58, true);
  const drop = uc(p, 'Thả vật phẩm', 1130, 520, 240, 58, true);
  const backpack = uc(p, 'Trang bị balo mở rộng', 1510, 350, 280, 58, true);
  edge(p, player, inv);
  edge(p, inv, grid, '<<include>>', 'include');
  [move, use, drop, backpack].forEach(v => edge(p, v, inv, '<<extend>>', 'extend'));

  const hotbar = uc(p, 'Quản lý Hotbar', 330, 720, 260);
  const assign = uc(p, 'Gán vật phẩm vào ô Hotbar', 740, 680, 300, 58, true);
  const select = uc(p, 'Chọn ô Hotbar', 1130, 650, 250, 58, true);
  const equip = uc(p, 'Trang bị vật phẩm đang chọn', 1510, 650, 300, 58, true);
  const remove = uc(p, 'Bỏ vật phẩm khỏi Hotbar', 1130, 770, 280, 58, true);
  edge(p, player, hotbar);
  edge(p, hotbar, assign, '<<include>>', 'include');
  edge(p, hotbar, select, '<<include>>', 'include');
  edge(p, select, equip, '<<include>>', 'include');
  edge(p, remove, hotbar, '<<extend>>', 'extend');
  const n1 = note(p, 'Lấy / cất là các nhánh tùy chọn mở rộng từ lúc đang xem nội dung container.', 380, 990, 360, 115);
  const n2 = note(p, 'Mỗi hành động độc lập của người chơi có association nét liền; include/extend chỉ mô tả quan hệ giữa use case.', 900, 990, 400, 125);
  edge(p, n1, take, '', 'note');
  edge(p, n2, inv, '', 'note');
}

function combatPage() {
  const p = makePage('12 - Chiến đấu, tiếng động và Zombie', 2100, 1350);
  boundary(p, 'USE CASE — CHIẾN ĐẤU, ĐẠN DƯỢC, TIẾNG ĐỘNG VÀ ZOMBIE', 230, 45, 1820, 1220);
  const player = actor(p, 'Người chơi', 65, 520);
  const combat = uc(p, 'Tham gia chiến đấu', 330, 100, 280);
  const equip = uc(p, 'Trang bị vũ khí', 740, 80, 260, 58, true);
  const target = uc(p, 'Xác định hướng tấn công', 740, 200, 290, 58, true);
  edge(p, player, combat);
  edge(p, combat, equip, '<<include>>', 'include');
  edge(p, combat, target, '<<include>>', 'include');

  const melee = uc(p, 'Tấn công cận chiến', 330, 440, 270);
  const shoot = uc(p, 'Bắn súng', 330, 720, 250);
  edge(p, melee, combat, '<<extend>>', 'extend');
  edge(p, shoot, combat, '<<extend>>', 'extend');
  edge(p, player, melee);
  edge(p, player, shoot);

  const meleeHit = uc(p, 'Kiểm tra va chạm cận chiến', 740, 400, 310, 58, true);
  const gunHit = uc(p, 'Kiểm tra đường đạn', 740, 670, 270, 58, true);
  const ammo = uc(p, 'Tiêu hao đạn trong súng', 740, 780, 280, 58, true);
  const reload = uc(p, 'Nạp đạn', 740, 890, 240, 58, true);
  edge(p, melee, meleeHit, '<<include>>', 'include');
  edge(p, shoot, gunHit, '<<include>>', 'include');
  edge(p, shoot, ammo, '<<include>>', 'include');
  edge(p, reload, shoot, '<<extend>>', 'extend');

  const damage = uc(p, 'Gây sát thương Zombie', 1160, 500, 290);
  const kill = uc(p, 'Hạ Zombie', 1570, 470, 250, 58, true);
  edge(p, meleeHit, damage, '<<include>>', 'include');
  edge(p, gunHit, damage, '<<include>>', 'include');
  edge(p, kill, damage, '<<extend>>', 'extend');

  const noise = uc(p, 'Phát sinh tiếng động', 1160, 760, 280);
  const detect = uc(p, 'Zombie phát hiện nguồn âm', 1570, 700, 300, 58, true);
  const chase = uc(p, 'Zombie truy đuổi người chơi', 1570, 820, 300, 58, true);
  edge(p, melee, noise, '<<include>>', 'include');
  edge(p, shoot, noise, '<<include>>', 'include');
  edge(p, detect, noise, '<<extend>>', 'extend');
  edge(p, detect, chase, '<<include>>', 'include');
  const n1 = note(p, 'Cận chiến và bắn súng là hai biến thể tùy chọn mở rộng use case chiến đấu.', 350, 1030, 360, 115);
  const n2 = note(p, 'Zombie chỉ phát hiện khi tiếng động nằm trong phạm vi / thỏa ngưỡng nên dùng «extend».', 980, 1030, 380, 115);
  edge(p, n1, melee, '', 'note');
  edge(p, n2, detect, '', 'note');
}

function multiplayerInteractionPage() {
  const p = makePage('13 - Tương tác Multiplayer', 1900, 1200);
  boundary(p, 'USE CASE — PHỐI HỢP VÀ TƯƠNG TÁC MULTIPLAYER', 220, 45, 1630, 1080);
  const player = actor(p, 'Người chơi', 60, 500);
  const communicate = uc(p, 'Giao tiếp đồng đội', 300, 120, 250);
  const trade = uc(p, 'Trao đổi vật phẩm', 300, 500, 250);
  const team = uc(p, 'Phối hợp sinh tồn', 300, 850, 250);
  [communicate, trade, team].forEach(v => edge(p, player, v));
  const chat = uc(p, 'Mở Chat', 650, 70, 210, 55, true);
  const type = uc(p, 'Nhập và gửi tin nhắn', 650, 150, 240, 55, true);
  const voice = uc(p, 'Bật Push-to-talk', 650, 230, 220, 55, true);
  const hear = uc(p, 'Nghe voice theo khoảng cách', 650, 310, 270, 55, true);
  [chat, voice].forEach(v => edge(p, communicate, v, '<<include>>', 'include'));
  edge(p, chat, type, '<<include>>', 'include');
  edge(p, voice, hear, '<<include>>', 'include');
  const voiceNoise = uc(p, 'Voice tạo tiếng động cho zombie', 1010, 230, 290, 55, true);
  edge(p, voiceNoise, voice, '<<extend>>', 'extend');

  const request = uc(p, 'Gửi yêu cầu Trade', 650, 460, 230, 55, true);
  const accept = uc(p, 'Chấp nhận / từ chối', 650, 540, 230, 55, true);
  const choose = uc(p, 'Chọn vật phẩm trao đổi', 650, 620, 250, 55, true);
  const lock = uc(p, 'Khóa lựa chọn', 1010, 500, 220, 55, true);
  const confirm = uc(p, 'Hai bên xác nhận Trade', 1010, 580, 250, 55, true);
  const cancel = uc(p, 'Hủy giao dịch', 1010, 660, 220, 55, true);
  [request, accept, choose].forEach(v => edge(p, trade, v, '<<include>>', 'include'));
  edge(p, choose, lock, '<<include>>', 'include');
  edge(p, lock, confirm, '<<include>>', 'include');
  edge(p, cancel, trade, '<<extend>>', 'extend');

  const quest = uc(p, 'Chia sẻ tiến độ nhiệm vụ', 650, 820, 260, 55, true);
  const map = uc(p, 'Mở bản đồ cho toàn đội', 650, 900, 250, 55, true);
  const role = uc(p, 'Phân chia vai trò', 1010, 820, 220, 55, true);
  const defend = uc(p, 'Bảo vệ đồng đội', 1010, 900, 220, 55, true);
  const spectate = uc(p, 'Theo dõi đồng đội khi chết', 1370, 820, 270, 55, true);
  const respawn = uc(p, 'Respawn vào phiên chơi', 1370, 900, 250, 55, true);
  [quest, map, role, defend].forEach(v => edge(p, team, v, '<<include>>', 'include'));
  edge(p, spectate, team, '<<extend>>', 'extend');
  edge(p, respawn, spectate, '<<extend>>', 'extend');
}

function mainQuestPart1Page() {
  const p = makePage('14 - Main Quest phần 1', 1900, 1200);
  boundary(p, 'USE CASE — MAIN QUEST PHẦN 1: TÌM BẢN ĐỒ VÀ KHU QUÂN SỰ', 220, 45, 1630, 1080);
  const player = actor(p, 'Người chơi', 60, 500);
  const begin = uc(p, 'Bắt đầu Main Play', 300, 100, 250);
  edge(p, player, begin);
  const spawn = uc(p, 'Spawn tại rìa thành phố', 650, 70, 250, 55, true);
  const objective = uc(p, 'Nhận mục tiêu tìm quân đội', 650, 150, 270, 55, true);
  edge(p, begin, spawn, '<<include>>', 'include');
  edge(p, begin, objective, '<<include>>', 'include');
  const follow = uc(p, 'Theo dấu vết đến văn phòng', 300, 310, 280);
  edge(p, player, follow);
  const survive = uc(p, 'Loot vật tư trên đường', 650, 270, 240, 55, true);
  const fight = uc(p, 'Chiến đấu / né zombie', 650, 350, 240, 55, true);
  edge(p, survive, follow, '<<extend>>', 'extend');
  edge(p, fight, follow, '<<extend>>', 'extend');
  const office = uc(p, 'Đi vào Khu vực 2: Văn phòng', 300, 520, 290);
  edge(p, player, office);
  const activate = uc(p, 'Kích hoạt quest tìm bản đồ', 650, 470, 270, 55, true);
  const markers = uc(p, 'Hiện chấm vàng trên tủ nhiệm vụ', 650, 550, 300, 55, true);
  const search = uc(p, 'Nhấn E kiểm tra từng tủ', 650, 630, 260, 55, true);
  edge(p, office, activate, '<<include>>', 'include');
  edge(p, activate, markers, '<<include>>', 'include');
  edge(p, activate, search, '<<include>>', 'include');
  const empty = uc(p, 'Nhận kết quả tủ trống', 1010, 550, 240, 55, true);
  const found = uc(p, 'Tìm thấy bản đồ', 1010, 650, 230);
  edge(p, empty, search, '<<extend>>', 'extend');
  edge(p, found, search, '<<extend>>', 'extend');
  const unlock = uc(p, 'Mở khóa bản đồ cho toàn đội', 1340, 570, 300, 55, true);
  const camera = uc(p, 'Camera lia tới Khu vực 3', 1340, 660, 270, 55, true);
  const newQuest = uc(p, 'Nhận quest điều tra khu quân sự', 1340, 750, 300, 55, true);
  edge(p, found, unlock, '<<include>>', 'include');
  edge(p, found, camera, '<<include>>', 'include');
  edge(p, found, newQuest, '<<include>>', 'include');
  const base = uc(p, 'Đến Khu vực 3: Khu quân sự', 1010, 850, 300);
  const abandoned = uc(p, 'Phát hiện căn cứ bị bỏ hoang', 1340, 860, 300, 55, true);
  edge(p, player, base);
  edge(p, base, abandoned, '<<include>>', 'include');
  const n = note(p, 'Host chọn đúng một tủ ngẫu nhiên; bản đồ luôn xuất hiện và tiến độ được đồng bộ cả phòng.', 1380, 240, 320, 130);
  edge(p, n, search, '', 'note');
}

function mainQuestFinalePage() {
  const p = makePage('15 - Main Quest finale', 2000, 1250);
  boundary(p, 'USE CASE — MAIN QUEST FINALE: PHÒNG THỦ, SỬA XE VÀ THOÁT THÂN', 220, 45, 1730, 1120);
  const player = actor(p, 'Người chơi', 55, 480);
  const solo = actor(p, 'Người chơi Solo', 55, 760);
  const team = actor(p, 'Đồng đội Multiplayer', 55, 960);
  edge(p, solo, player, '', 'general'); edge(p, team, player, '', 'general');
  const investigate = uc(p, 'Điều tra khu quân sự', 300, 100, 270);
  edge(p, player, investigate);
  const command = uc(p, 'Kiểm tra phòng chỉ huy', 650, 60, 250, 55, true);
  const noSurvivor = uc(p, 'Xác nhận không còn người sống', 650, 140, 290, 55, true);
  const car = uc(p, 'Tìm xe thoát thân trong hàng rào', 650, 220, 310, 55, true);
  [command, noSurvivor, car].forEach(v => edge(p, investigate, v, '<<include>>', 'include'));
  const startCar = uc(p, 'Thử khởi động xe', 300, 360, 250);
  edge(p, player, startCar);
  const alarm = uc(p, 'Kích hoạt báo động chống trộm', 650, 340, 300, 55, true);
  const attract = uc(p, 'Thu hút đàn zombie', 1010, 340, 240, 55, true);
  const closeGate = uc(p, 'Đóng cổng khu quân sự', 1340, 340, 260, 55, true);
  edge(p, startCar, alarm, '<<include>>', 'include');
  edge(p, alarm, attract, '<<include>>', 'include');
  edge(p, attract, closeGate, '<<include>>', 'include');
  const siege = uc(p, 'Thực hiện nhiệm vụ sinh tồn có thời gian', 300, 590, 330);
  edge(p, player, siege);
  const soloDef = uc(p, 'Solo: cổng chịu áp lực khoảng 3 phút', 650, 520, 330, 55, true);
  const multiDef = uc(p, 'Multiplayer: cổng có HP riêng', 650, 610, 300, 55, true);
  edge(p, solo, soloDef);
  edge(p, team, multiDef);
  edge(p, soloDef, siege, '<<extend>>', 'extend');
  edge(p, multiDef, siege, '<<extend>>', 'extend');
  const defend = uc(p, 'Phòng thủ cổng', 1010, 500, 230, 55, true);
  const search = uc(p, 'Tìm phụ tùng xe', 1010, 590, 230, 55, true);
  const protect = uc(p, 'Bảo vệ người sửa xe', 1010, 680, 250, 55, true);
  [defend, search, protect].forEach(v => edge(p, siege, v, '<<include>>', 'include'));
  const repair = uc(p, 'Sửa xe thoát thân', 300, 830, 260);
  edge(p, player, repair);
  const battery = uc(p, 'Lắp ắc quy', 650, 780, 210, 55, true);
  const fuel = uc(p, 'Bổ sung nhiên liệu', 650, 860, 220, 55, true);
  const parts = uc(p, 'Lắp bộ dụng cụ / phụ tùng', 650, 940, 280, 55, true);
  const interrupt = uc(p, 'Bị tấn công làm gián đoạn sửa', 1010, 860, 290, 55, true);
  [battery, fuel, parts].forEach(v => edge(p, repair, v, '<<include>>', 'include'));
  edge(p, interrupt, repair, '<<extend>>', 'extend');
  const ready = uc(p, 'Hoàn thành sửa xe', 1340, 780, 240);
  const escape = uc(p, 'Lái xe rời khu vực', 1340, 880, 250);
  const ending = uc(p, 'Kết thúc Main Play', 1660, 880, 240);
  edge(p, repair, ready, '<<include>>', 'include');
  edge(p, ready, escape, '<<include>>', 'include');
  edge(p, escape, ending, '<<include>>', 'include');
  edge(p, player, escape);
  const fail = uc(p, 'Cổng vỡ / cả đội tử vong', 1340, 1010, 280, 55, true);
  edge(p, fail, siege, '<<extend>>', 'extend');
}

function settingsPage() {
  const p = makePage('16 - Options và Pause', 2000, 1250);
  boundary(p, 'USE CASE — OPTIONS, PAUSE MENU VÀ THOÁT PHIÊN CHƠI', 220, 45, 1730, 1120);
  const player = actor(p, 'Người chơi', 55, 500);
  const options = uc(p, 'Mở Options', 300, 100, 240);
  edge(p, player, options);
  const display = uc(p, 'Thiết lập Display', 650, 70, 230);
  const controls = uc(p, 'Thiết lập Controls', 650, 320, 230);
  const audio = uc(p, 'Thiết lập Audio', 650, 500, 230);
  [display, controls, audio].forEach(v => edge(p, options, v, '<<include>>', 'include'));
  const resolution = uc(p, 'Độ phân giải', 1010, 45, 210, 55, true);
  const mode = uc(p, 'Chế độ cửa sổ', 1010, 115, 210, 55, true);
  const quality = uc(p, 'Quality / Shadow / Anti-aliasing', 1010, 185, 300, 55, true);
  const fps = uc(p, 'FPS limit / hiện FPS / vị trí FPS', 1010, 255, 310, 55, true);
  const bright = uc(p, 'Độ sáng', 1370, 115, 190, 55, true);
  [resolution, mode, quality, fps, bright].forEach(v => edge(p, display, v, '<<include>>', 'include'));
  const sens = uc(p, 'Độ nhạy điều khiển', 1010, 340, 240, 55, true);
  const zoom = uc(p, 'Độ nhạy zoom', 1370, 340, 210, 55, true);
  edge(p, controls, sens, '<<include>>', 'include');
  edge(p, controls, zoom, '<<include>>', 'include');
  const master = uc(p, 'Âm lượng tổng', 1010, 490, 210, 55, true);
  const music = uc(p, 'Âm lượng nhạc', 1010, 560, 210, 55, true);
  const sfx = uc(p, 'Âm lượng hiệu ứng', 1370, 525, 230, 55, true);
  [master, music, sfx].forEach(v => edge(p, audio, v, '<<include>>', 'include'));
  const save = uc(p, 'Lưu thiết lập', 650, 690, 220);
  const back = uc(p, 'Quay lại', 1010, 690, 210);
  const unsaved = uc(p, 'Xử lý thay đổi chưa lưu', 1370, 690, 260, 55, true);
  edge(p, player, save); edge(p, player, back);
  edge(p, unsaved, back, '<<extend>>', 'extend');
  const pause = uc(p, 'Mở Pause Menu', 300, 860, 240);
  edge(p, player, pause);
  const resume = uc(p, 'Resume', 650, 820, 210, 55, true);
  const pauseOptions = uc(p, 'Mở Options trong game', 650, 900, 250, 55, true);
  const quitSession = uc(p, 'Quit phiên chơi', 650, 980, 220, 55, true);
  edge(p, pause, resume, '<<include>>', 'include');
  edge(p, pause, pauseOptions, '<<include>>', 'include');
  edge(p, pause, quitSession, '<<include>>', 'include');
  const menu = uc(p, 'Trở về Main Menu', 1010, 980, 240, 55, true);
  edge(p, quitSession, menu, '<<include>>', 'include');
}

function navigationActivityPage() {
  const p = makePage('17 - Activity Flow toàn game', 2100, 1300);
  vertex(p, 'ACTIVITY FLOW — ĐIỀU HƯỚNG TOÀN GAME', 550, 20, 1000, 50, S.title);
  const start = vertex(p, '', 100, 100, 34, 34, S.start);
  const menu = activity(p, 'Main Menu', 240, 85, 250, 65);
  const choice = decision(p, 'Người chơi chọn?', 600, 70, 190, 100);
  edge(p, start, menu, '', 'flow'); edge(p, menu, choice, '', 'flow');
  const solo = activity(p, 'SOLO', 910, 80, 210);
  const tutorial = activity(p, 'HƯỚNG DẪN', 910, 250, 230);
  const multi = activity(p, 'MULTIPLAYER', 910, 440, 230);
  const options = activity(p, 'OPTIONS', 910, 650, 210);
  const credits = activity(p, 'CREDITS', 910, 830, 210);
  const quit = activity(p, 'QUIT', 910, 1010, 210);
  edge(p, choice, solo, 'Solo', 'flow'); edge(p, choice, tutorial, 'Hướng dẫn', 'flow');
  edge(p, choice, multi, 'Multiplayer', 'flow'); edge(p, choice, options, 'Options', 'flow');
  edge(p, choice, credits, 'Credits', 'flow'); edge(p, choice, quit, 'Quit', 'flow');
  const diff = activity(p, 'Chọn độ khó', 1240, 100, 230);
  const char = activity(p, 'Chọn nhân vật + nhập tên', 1540, 100, 280);
  const loadSolo = activity(p, 'Loading', 1870, 100, 180);
  edge(p, solo, diff, '', 'flow'); edge(p, diff, char, '', 'flow'); edge(p, char, loadSolo, '', 'flow');
  const intro = activity(p, 'Cinematic + chuỗi hướng dẫn', 1240, 230, 290);
  const tutEnd = decision(p, 'Kết thúc Tutorial', 1600, 215, 190, 100);
  edge(p, tutorial, intro, '', 'flow'); edge(p, intro, tutEnd, '', 'flow');
  const startMain = activity(p, 'Bắt đầu sinh tồn', 1870, 170, 190, 55);
  const replay = activity(p, 'Chơi lại Tutorial', 1870, 250, 190, 55);
  const backMenu = activity(p, 'Trở về Menu', 1870, 330, 190, 55);
  edge(p, tutEnd, startMain, '', 'flow'); edge(p, tutEnd, replay, '', 'flow'); edge(p, tutEnd, backMenu, '', 'flow');
  edge(p, replay, intro, '', 'flow'); edge(p, backMenu, menu, '', 'flow');
  const hostJoin = decision(p, 'Host hay Join?', 1240, 410, 190, 100);
  const host = activity(p, 'Cấu hình và tạo phòng', 1540, 390, 260);
  const join = activity(p, 'Chọn và tham gia phòng', 1540, 500, 260);
  const select = activity(p, 'Chọn nhân vật + nhập tên', 1870, 445, 280);
  const waiting = activity(p, 'Phòng chờ', 1870, 560, 210);
  const hostStart = activity(p, 'Host bấm START', 1870, 660, 210);
  edge(p, multi, hostJoin, '', 'flow'); edge(p, hostJoin, host, 'Host', 'flow'); edge(p, hostJoin, join, 'Join', 'flow');
  edge(p, host, select, '', 'flow'); edge(p, join, select, '', 'flow'); edge(p, select, waiting, '', 'flow'); edge(p, waiting, hostStart, '', 'flow');
  const settings = activity(p, 'Chỉnh Display / Controls / Audio', 1240, 650, 320);
  const save = decision(p, 'Lưu thay đổi?', 1640, 630, 190, 100);
  edge(p, options, settings, '', 'flow'); edge(p, settings, save, '', 'flow'); edge(p, save, menu, 'Lưu / quay lại', 'flow');
  const viewCredits = activity(p, 'Xem thông tin nhóm', 1240, 830, 260);
  edge(p, credits, viewCredits, '', 'flow'); edge(p, viewCredits, menu, 'Back', 'flow');
  const endApp = vertex(p, '', 1320, 1025, 40, 40, S.end);
  edge(p, quit, endApp, '', 'flow');
  const mainPlay = activity(p, 'MAIN PLAY', 1770, 870, 260, 75);
  edge(p, loadSolo, mainPlay, '', 'flow'); edge(p, startMain, mainPlay, '', 'flow'); edge(p, hostStart, mainPlay, 'Loading', 'flow');
  const finish = decision(p, 'Kết thúc / rời game?', 1770, 1020, 260, 110);
  edge(p, mainPlay, finish, '', 'flow'); edge(p, finish, menu, 'Trở về Menu', 'flow');
  const endGame = vertex(p, '', 2030, 1060, 40, 40, S.end);
  edge(p, finish, endGame, 'Đóng game', 'flow');
}

function tutorialActivityPage() {
  const p = makePage('18 - Activity Flow Tutorial', 2200, 1450);
  vertex(p, 'ACTIVITY FLOW — SCENE HƯỚNG DẪN CHI TIẾT', 600, 15, 1000, 50, S.title);
  const xs = [120, 480, 840, 1200, 1560, 1920];
  const rows = [100, 260, 420, 580, 740, 900];
  const steps = [
    ['Bắt đầu Tutorial', 'Mở mắt trong xe', 'Nghe thông báo chính phủ', 'Camera theo xe chạy', 'Xe gặp sự cố', 'Hội thoại và rời xe'],
    ['Spawn người chơi', 'Học di chuyển WASD', 'Học zoom camera', 'Học quan sát', 'Theo dõi đói / khát', 'Đi tới căn nhà'],
    ['Mở tủ bếp', 'Lấy toàn bộ vật phẩm', 'Ăn và uống', 'Đưa S12K vào Hotbar', 'Nạp đạn', 'Rời căn nhà'],
    ['Camera giới thiệu Zombie A', 'Theo dõi thanh độ ồn', 'Ngồi và lén tiếp cận', 'Hạ Zombie A bằng cận chiến', 'Nhận vết thương', 'Mở Health Status'],
    ['Băng bó vết thương', 'Camera giới thiệu Zombie B', 'Đọc cảnh báo tiếng súng', 'Bắn hạ Zombie B', 'Tiếng súng gọi đàn zombie', 'Đàn zombie bao vây'],
    ['Chiến đấu sống sót', 'Người chơi tử vong', 'Hiện kết thúc Tutorial', 'Chọn hành động tiếp theo']
  ];
  let prev = vertex(p, '', 40, 110, 34, 34, S.start);
  const created = [];
  for (let r = 0; r < steps.length; r++) {
    const direction = r % 2 === 0 ? [...Array(6).keys()] : [...Array(6).keys()].reverse();
    for (let i = 0; i < steps[r].length; i++) {
      const c = direction[i];
      const label = steps[r][i];
      if (!label) continue;
      const id = label === 'Chọn hành động tiếp theo'
        ? decision(p, label, xs[c], rows[r] - 15, 230, 100)
        : activity(p, label, xs[c], rows[r], 250, 62, r === 0 && c < 6);
      edge(p, prev, id, '', 'flow');
      prev = id;
      created.push({ label, id });
    }
  }
  const choice = created.find(x => x.label === 'Chọn hành động tiếp theo').id;
  const intro = created.find(x => x.label === 'Mở mắt trong xe').id;
  const main = activity(p, 'Bắt đầu Main Play', 480, 1110, 250);
  const replay = activity(p, 'Chơi lại Tutorial', 920, 1110, 250);
  const menu = activity(p, 'Trở về Main Menu', 1360, 1110, 250);
  edge(p, choice, main, 'Bắt đầu sinh tồn', 'flow');
  edge(p, choice, replay, 'Chơi lại', 'flow');
  edge(p, choice, menu, 'Trở về Menu', 'flow');
  edge(p, replay, intro, '', 'flow');
  const end = vertex(p, '', 1020, 1280, 40, 40, S.end);
  edge(p, main, end, 'Sang Main Play', 'flow');
  edge(p, menu, end, 'Kết thúc Tutorial', 'flow');
}

function mainPlayActivityPage() {
  const p = makePage('19 - Activity Flow Main Play', 2300, 1500);
  vertex(p, 'ACTIVITY FLOW — MAIN PLAY HOÀN CHỈNH', 650, 15, 1000, 50, S.title);
  const start = vertex(p, '', 40, 110, 34, 34, S.start);
  const a = activity(p, 'Spawn tại rìa thành phố', 120, 90, 260);
  const b = activity(p, 'Nhận mục tiêu tìm dấu vết quân đội', 450, 90, 300);
  const c = activity(p, 'Đi tới Khu vực 2: Văn phòng', 820, 90, 300);
  const d = activity(p, 'Kích hoạt quest tìm bản đồ', 1190, 90, 290);
  const e = activity(p, 'Hiện chấm vàng trên các tủ', 1550, 90, 290);
  const f = activity(p, 'Kiểm tra tủ bằng phím E', 1910, 90, 270);
  [ [start,a],[a,b],[b,c],[c,d],[d,e],[e,f] ].forEach(([x,y])=>edge(p,x,y,'','flow'));
  const found = decision(p, 'Tìm thấy bản đồ?', 1910, 230, 240, 110);
  edge(p, f, found, '', 'flow');
  edge(p, found, f, 'Chưa tìm thấy', 'flow');
  const unlock = activity(p, 'Mở khóa bản đồ cho toàn đội', 1550, 250, 300);
  const focus = activity(p, 'Camera lia tới Khu vực 3', 1190, 250, 290);
  const newQuest = activity(p, 'Nhận quest điều tra khu quân sự', 820, 250, 310);
  const base = activity(p, 'Đi tới Khu vực 3', 450, 250, 260);
  const abandoned = activity(p, 'Phát hiện căn cứ bị bỏ hoang', 120, 250, 300);
  edge(p, found, unlock, 'Đã tìm thấy', 'flow'); edge(p, unlock, focus, '', 'flow'); edge(p, focus, newQuest, '', 'flow'); edge(p, newQuest, base, '', 'flow'); edge(p, base, abandoned, '', 'flow');
  const command = activity(p, 'Kiểm tra phòng chỉ huy', 120, 440, 280);
  const car = activity(p, 'Tìm xe thoát thân trong hàng rào', 470, 440, 320);
  const startCar = activity(p, 'Thử khởi động xe', 860, 440, 250);
  const alarm = activity(p, 'Báo động chống trộm vang lên', 1180, 440, 310);
  const horde = activity(p, 'Đàn zombie kéo tới', 1560, 440, 260);
  const gate = activity(p, 'Đóng cổng khu quân sự', 1890, 440, 280);
  edge(p, abandoned, command, '', 'flow'); edge(p, command, car, '', 'flow'); edge(p, car, startCar, '', 'flow'); edge(p, startCar, alarm, '', 'flow'); edge(p, alarm, horde, '', 'flow'); edge(p, horde, gate, '', 'flow');
  const mode = decision(p, 'Chế độ chơi?', 1890, 640, 240, 110);
  edge(p, gate, mode, '', 'flow');
  const solo = activity(p, 'Solo: cổng chịu áp lực khoảng 3 phút', 1450, 640, 340);
  const multi = activity(p, 'Multiplayer: cổng có HP riêng', 1450, 790, 320);
  edge(p, mode, solo, 'Solo', 'flow'); edge(p, mode, multi, 'Multiplayer', 'flow');
  const soloLoop = activity(p, 'Luân phiên phòng thủ / tìm đồ / sửa xe', 1000, 640, 350);
  const roles = activity(p, 'Chia vai: thủ cổng / tìm đồ / sửa xe', 1000, 790, 350);
  edge(p, solo, soloLoop, '', 'flow'); edge(p, multi, roles, '', 'flow');
  const parts = activity(p, 'Thu thập ắc quy + nhiên liệu + phụ tùng', 520, 710, 390);
  edge(p, soloLoop, parts, '', 'flow'); edge(p, roles, parts, '', 'flow');
  const repair = activity(p, 'Giữ tương tác để lắp / sửa xe', 120, 710, 320);
  edge(p, parts, repair, '', 'flow');
  const attacked = decision(p, 'Bị zombie tấn công?', 120, 900, 260, 110);
  edge(p, repair, attacked, '', 'flow');
  const defend = activity(p, 'Dừng sửa và tự vệ', 500, 920, 260);
  edge(p, attacked, defend, 'Có', 'flow'); edge(p, defend, repair, 'Quay lại sửa', 'flow');
  const repaired = decision(p, 'Xe đã sửa hoàn tất?', 900, 900, 260, 110);
  edge(p, attacked, repaired, 'Không', 'flow'); edge(p, repaired, repair, 'Chưa', 'flow');
  const gateState = decision(p, 'Cổng còn trụ được?', 1300, 900, 260, 110);
  edge(p, repaired, gateState, 'Đang sửa', 'flow');
  const fail = activity(p, 'Cổng vỡ / cả đội tử vong', 1680, 920, 300);
  edge(p, gateState, fail, 'Không', 'flow');
  const escape = activity(p, 'Lên xe và lái khỏi khu vực', 1300, 1110, 300);
  edge(p, repaired, escape, 'Đã hoàn tất', 'flow'); edge(p, gateState, escape, 'Có + xe đã xong', 'flow');
  const ending = activity(p, 'Cinematic / thống kê kết thúc', 900, 1110, 300);
  const end = vertex(p, '', 740, 1125, 40, 40, S.end);
  edge(p, escape, ending, '', 'flow'); edge(p, ending, end, 'Hoàn thành game', 'flow');
  const failEnd = vertex(p, '', 2050, 940, 40, 40, S.end);
  edge(p, fail, failEnd, 'Thất bại', 'flow');
}

overviewPage();
menuPage();
soloPage();
multiplayerHostPage();
multiplayerClientPage();
tutorialOpeningPage();
tutorialTrainingPage();
explorationPage();
survivalPage();
healthPage();
inventoryPage();
combatPage();
multiplayerInteractionPage();
mainQuestPart1Page();
mainQuestFinalePage();
settingsPage();
navigationActivityPage();
tutorialActivityPage();
mainPlayActivityPage();

function pageXml(p) {
  return `<mxGraphModel dx="${p.width}" dy="${p.height}" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="${p.width}" pageHeight="${p.height}" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/>${p.cells.join('')}</root></mxGraphModel>`;
}

function compress(xml) {
  return zlib.deflateRawSync(Buffer.from(encodeURIComponent(xml), 'utf8')).toString('base64');
}

const diagrams = pages.map((p, index) => `<diagram id="fos-${String(index + 1).padStart(2, '0')}" name="${esc(p.name)}">${compress(pageXml(p))}</diagram>`).join('');
const file = `<mxfile host="app.diagrams.net" modified="2026-08-12T12:00:00.000Z" agent="draw.io" version="26.0.14" type="device" pages="${pages.length}">${diagrams}</mxfile>`;
fs.writeFileSync(OUT, file, 'utf8');

const totalCells = pages.reduce((sum, p) => sum + p.cells.length + 2, 0);
console.log(`Created ${OUT}`);
console.log(`Pages: ${pages.length}`);
console.log(`Total mxCells: ${totalCells}`);
