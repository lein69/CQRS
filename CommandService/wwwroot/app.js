const queryApi = new URLSearchParams(window.location.search).get("queryApi")
  || "http://localhost:5135";
const groupsElement = document.querySelector("#groups");
const ownedGroupsElement = document.querySelector("#owned-groups");
const joinedGroupsElement = document.querySelector("#joined-groups");
const pendingRequestsElement = document.querySelector("#pending-requests");
const noticeElement = document.querySelector("#notice");
const pageLabel = document.querySelector("#page-label");
const pageSize = 20;
let currentPage = 1;
let currentSearch = "";
let totalCount = 0;
let noticeTimeout;
let accessToken = sessionStorage.getItem("studyGroupAccessToken");
let currentUser = null;

try {
  currentUser = JSON.parse(sessionStorage.getItem("studyGroupUser") || "null");
} catch {
  sessionStorage.removeItem("studyGroupAccessToken");
  sessionStorage.removeItem("studyGroupUser");
  accessToken = null;
}
if (!accessToken || !currentUser) {
  sessionStorage.removeItem("studyGroupAccessToken");
  sessionStorage.removeItem("studyGroupUser");
  accessToken = null;
  currentUser = null;
}

function showNotice(message, isError = false) {
  noticeElement.textContent = message;
  noticeElement.classList.add("visible");
  noticeElement.classList.toggle("error", isError);
  window.clearTimeout(noticeTimeout);
  noticeTimeout = window.setTimeout(() => noticeElement.classList.remove("visible"), 4500);
}

function clearSession() {
  accessToken = null;
  currentUser = null;
  sessionStorage.removeItem("studyGroupAccessToken");
  sessionStorage.removeItem("studyGroupUser");
  selectAuthForm("login");
  updateAuthUi();
}

function saveSession(response) {
  accessToken = response.accessToken;
  currentUser = response.user;
  sessionStorage.setItem("studyGroupAccessToken", accessToken);
  sessionStorage.setItem("studyGroupUser", JSON.stringify(currentUser));
  updateAuthUi();
}

function updateAuthUi() {
  const signedIn = Boolean(accessToken && currentUser);
  document.querySelector("#auth-view").hidden = signedIn;
  document.querySelector("#app-view").hidden = !signedIn;
  document.title = signedIn ? "Nhóm học tập | StudySpace" : "Đăng nhập | StudySpace";
  if (signedIn) {
    document.querySelector("#account-name").textContent = currentUser.displayName;
    document.querySelector("#account-email").textContent = currentUser.email;
    loadGroups().catch((error) => showNotice(error.message, true));
  }
}

function selectAuthForm(name) {
  const showLogin = name === "login";
  document.querySelector("#login-form").hidden = !showLogin;
  document.querySelector("#register-form").hidden = showLogin;
  document.querySelector("#show-login").classList.toggle("is-active", showLogin);
  document.querySelector("#show-register").classList.toggle("is-active", !showLogin);
  document.querySelector("#show-login").setAttribute("aria-selected", showLogin);
  document.querySelector("#show-register").setAttribute("aria-selected", !showLogin);
  document.querySelector("#auth-heading").textContent = showLogin
    ? "Chào mừng trở lại"
    : "Tạo tài khoản mới";
  document.querySelector("#auth-description").textContent = showLogin
    ? "Đăng nhập để tiếp tục hành trình học tập của bạn."
    : "Tham gia cộng đồng và tìm nhóm học tập phù hợp.";
}

async function requestJson(url, options = {}) {
  const headers = {
    "Content-Type": "application/json",
    ...options.headers
  };
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  const response = await fetch(url, { ...options, headers });
  const text = await response.text();
  let body = null;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      throw new Error("Máy chủ trả về nội dung không phải JSON.");
    }
  }

  if (!response.ok) {
    if (response.status === 401 && accessToken) {
      clearSession();
      throw new Error("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    }
    throw new Error(
      body?.detail || body?.message || body?.title || `HTTP ${response.status}`
    );
  }

  return body;
}

function addText(parent, tag, className, text) {
  const element = document.createElement(tag);
  if (className) {
    element.className = className;
  }
  element.textContent = text;
  parent.append(element);
  return element;
}

function addButton(parent, text, action, className = "button button-muted") {
  const button = document.createElement("button");
  button.type = "button";
  button.className = className;
  button.dataset.action = action;
  button.textContent = text;
  parent.append(button);
  return button;
}

function renderGroup(group, membershipRole = null) {
  const card = document.createElement("article");
  card.className = "group-card";
  card.dataset.groupId = group.id;

  addText(card, "h3", "", group.name);
  const badges = document.createElement("div");
  badges.className = "group-badges";
  addText(badges, "span", "group-badge", group.subject);
  addText(
    badges,
    "span",
    "group-badge group-badge-muted",
    {
      All: "Mọi trình độ",
      Beginner: "Mới bắt đầu",
      Intermediate: "Cơ bản",
      Advanced: "Nâng cao"
    }[group.level] || "Mọi trình độ"
  );
  card.append(badges);
  if (group.goal) {
    addText(card, "p", "group-goal", group.goal);
  }
  addText(
    card,
    "p",
    "group-description",
    group.description || "Chưa có mô tả."
  );

  const meta = document.createElement("div");
  meta.className = "group-meta";
  addText(meta, "span", "", `Thành viên: ${group.memberCount}/${group.maxMembers}`);
  addText(meta, "span", "", `Trạng thái: ${group.status}`);
  const meetingModes = {
    Online: "Trực tuyến",
    InPerson: "Gặp trực tiếp",
    Hybrid: "Linh hoạt"
  };
  addText(meta, "span", "", meetingModes[group.meetingMode] || "Trực tuyến");
  if (group.schedule) {
    addText(meta, "span", "", `Lịch: ${group.schedule}`);
  }
  if (group.meetingLocation) {
    addText(meta, "span", "", `Địa điểm: ${group.meetingLocation}`);
  }
  const isOwner = membershipRole === "Owner" || group.ownerId === currentUser?.id;
  if (membershipRole) {
    addText(
      meta,
      "span",
      "membership-label",
      membershipRole === "Owner" ? "Vai trò: Chủ nhóm" : "Vai trò: Thành viên"
    );
  }
  addText(
    meta,
    "span",
    "",
    `Chủ nhóm: ${group.ownerDisplayName || (isOwner ? currentUser.displayName : "Chủ nhóm")}`
  );
  card.append(meta);

  if (accessToken) {
    const actions = document.createElement("div");
    actions.className = "card-actions";
    if (isOwner) {
      addButton(actions, "Yêu cầu chờ duyệt", "show-requests");
      addButton(actions, "Xóa nhóm", "delete-group", "button button-danger");
    } else if (membershipRole === "Member") {
      addButton(actions, "Rời nhóm", "leave-group", "button button-danger");
    } else if (membershipRole !== "Member") {
      addButton(actions, "Gửi yêu cầu tham gia", "show-join");
    }
    if (actions.childElementCount > 0) {
      card.append(actions);
    }
  }
  return card;
}

function selectWorkspaceView(name) {
  document.querySelectorAll(".workspace-tab").forEach((tab) => {
    const isActive = tab.dataset.view === name;
    tab.classList.toggle("is-active", isActive);
    tab.setAttribute("aria-selected", isActive);
    tab.tabIndex = isActive ? 0 : -1;
  });
  document.querySelectorAll(".workspace-view").forEach((view) => {
    view.hidden = view.id !== `${name}-view`;
  });
}

async function loadGroups() {
  groupsElement.replaceChildren();
  addText(groupsElement, "div", "empty-state", "Đang tải nhóm...");
  const parameters = new URLSearchParams({
    page: currentPage.toString(),
    pageSize: pageSize.toString()
  });
  if (currentSearch) {
    parameters.set("search", currentSearch);
  }

  const result = await requestJson(`${queryApi}/api/groups?${parameters}`);
  totalCount = result.totalCount;
  groupsElement.replaceChildren();
  if (result.items.length === 0) {
    addText(groupsElement, "div", "empty-state", "Chưa tìm thấy nhóm nào.");
  } else {
    result.items.forEach((group) => groupsElement.append(renderGroup(group)));
  }
  pageLabel.textContent = `Trang ${currentPage} · ${totalCount} nhóm`;
  document.querySelector("#previous-page").disabled = currentPage <= 1;
  document.querySelector("#next-page").disabled = currentPage * pageSize >= totalCount;
}

function renderPendingRequest(item) {
  const card = document.createElement("article");
  card.className = "pending-request-card";
  const details = document.createElement("div");
  details.className = "pending-request-details";
  const groupName = item.group?.name || "Nhóm không còn hiển thị";
  addText(details, "h4", "", groupName);
  if (item.group?.subject) {
    addText(details, "p", "pending-request-subject", item.group.subject);
  }
  if (item.group?.schedule) {
    addText(details, "p", "pending-request-schedule", `Lịch dự kiến: ${item.group.schedule}`);
  }
  const createdAt = new Date(item.request.createdAtUtc);
  const dateText = Number.isNaN(createdAt.getTime())
    ? ""
    : new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium" }).format(createdAt);
  if (dateText) {
    addText(details, "p", "pending-request-date", `Đã gửi ngày ${dateText}`);
  }
  card.append(details);
  addText(card, "span", "pending-status", "Đang chờ duyệt");
  return card;
}

async function loadMyGroups() {
  ownedGroupsElement.replaceChildren();
  joinedGroupsElement.replaceChildren();
  pendingRequestsElement.replaceChildren();
  addText(ownedGroupsElement, "div", "empty-state", "Đang tải nhóm...");
  addText(joinedGroupsElement, "div", "empty-state", "Đang tải nhóm...");
  addText(pendingRequestsElement, "div", "empty-state", "Đang tải yêu cầu...");

  const result = await requestJson(`${queryApi}/api/groups/mine`);
  const ownedGroups = result.ownedGroups || [];
  const joinedGroups = result.joinedGroups || [];
  const pendingRequests = result.pendingRequests || [];

  document.querySelector("#owned-group-count").textContent = ownedGroups.length;
  document.querySelector("#joined-group-count").textContent = joinedGroups.length;
  document.querySelector("#pending-request-count").textContent = pendingRequests.length;
  document.querySelector("#pending-review-count").textContent = result.pendingReviewCount || 0;
  document.querySelector("#owned-section-count").textContent = `${ownedGroups.length} nhóm`;
  document.querySelector("#joined-section-count").textContent = `${joinedGroups.length} nhóm`;
  document.querySelector("#pending-section-count").textContent =
    `${pendingRequests.length} yêu cầu`;
  const totalCount =
    ownedGroups.length +
    joinedGroups.length +
    pendingRequests.length +
    (result.pendingReviewCount || 0);
  const tabCount = document.querySelector("#my-groups-count");
  tabCount.textContent = totalCount;
  tabCount.hidden = totalCount === 0;

  ownedGroupsElement.replaceChildren();
  if (ownedGroups.length === 0) {
    addText(ownedGroupsElement, "div", "empty-state", "Bạn chưa tạo nhóm nào.");
  } else {
    ownedGroups.forEach((group) => ownedGroupsElement.append(renderGroup(group, "Owner")));
  }

  joinedGroupsElement.replaceChildren();
  if (joinedGroups.length === 0) {
    addText(joinedGroupsElement, "div", "empty-state", "Bạn chưa tham gia nhóm nào.");
  } else {
    joinedGroups.forEach((group) => joinedGroupsElement.append(renderGroup(group, "Member")));
  }

  pendingRequestsElement.replaceChildren();
  if (pendingRequests.length === 0) {
    addText(pendingRequestsElement, "div", "empty-state", "Bạn không có yêu cầu nào đang chờ.");
  } else {
    pendingRequests.forEach((item) =>
      pendingRequestsElement.append(renderPendingRequest(item)));
  }
}

async function loadRequests(card) {
  const groupId = card.dataset.groupId;
  const requests = await requestJson(
    `${queryApi}/api/groups/${groupId}/join-requests?status=Pending`
  );
  const panel = document.createElement("section");
  panel.className = "request-list";
  addText(panel, "strong", "", `Yêu cầu đang chờ (${requests.length})`);

  if (requests.length === 0) {
    addText(panel, "p", "request-message", "Không có yêu cầu chờ duyệt.");
  }

  requests.forEach((request) => {
    const row = document.createElement("div");
    row.className = "request-item";
    const details = document.createElement("div");
    addText(details, "strong", "", "Yêu cầu tham gia");
    addText(details, "p", "request-message", request.message || "Không có lời nhắn.");
    row.append(details);

    const actions = document.createElement("div");
    actions.className = "request-actions";
    const approveButton = addButton(actions, "Duyệt", "approve", "button button-primary");
    approveButton.dataset.requestId = request.id;
    const rejectButton = addButton(actions, "Từ chối", "reject", "button button-danger");
    rejectButton.dataset.requestId = request.id;
    row.append(actions);
    panel.append(row);
  });

  card.querySelector(".request-list")?.remove();
  card.append(panel);
}

document.querySelector("#show-login").addEventListener("click", () => selectAuthForm("login"));
document.querySelector("#show-register").addEventListener("click", () => selectAuthForm("register"));
document.querySelectorAll(".workspace-tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    selectWorkspaceView(tab.dataset.view);
    if (tab.dataset.view === "mine") {
      loadMyGroups().catch((error) => showNotice(error.message, true));
    }
  });
  tab.addEventListener("keydown", (event) => {
    const tabs = [...document.querySelectorAll(".workspace-tab")];
    const index = tabs.indexOf(tab);
    let nextIndex;
    if (event.key === "ArrowRight") {
      nextIndex = (index + 1) % tabs.length;
    } else if (event.key === "ArrowLeft") {
      nextIndex = (index - 1 + tabs.length) % tabs.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = tabs.length - 1;
    } else {
      return;
    }

    event.preventDefault();
    tabs[nextIndex].focus();
    tabs[nextIndex].click();
  });
});
document.querySelector("#refresh-my-groups").addEventListener("click", () => {
  loadMyGroups()
    .then(() => showNotice("Danh sách nhóm của bạn đã được làm mới."))
    .catch((error) => showNotice(error.message, true));
});
document.querySelector("#logout-button").addEventListener("click", () => {
  clearSession();
  showNotice("Bạn đã đăng xuất.");
});

document.querySelector("#login-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const values = new FormData(form);
  try {
    const response = await requestJson(`${window.location.origin}/api/auth/login`, {
      method: "POST",
      body: JSON.stringify({
        email: values.get("email"),
        password: values.get("password")
      })
    });
    saveSession(response);
    form.reset();
    showNotice(`Xin chào ${currentUser.displayName}.`);
  } catch (error) {
    showNotice(error.message, true);
  }
});

document.querySelector("#register-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const values = new FormData(form);
  try {
    const response = await requestJson(`${window.location.origin}/api/auth/register`, {
      method: "POST",
      body: JSON.stringify({
        displayName: values.get("displayName"),
        email: values.get("email"),
        password: values.get("password")
      })
    });
    saveSession(response);
    form.reset();
    showNotice(`Tài khoản đã tạo. Xin chào ${currentUser.displayName}.`);
  } catch (error) {
    showNotice(error.message, true);
  }
});

document.querySelector("#create-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const formElement = event.currentTarget;
  const form = new FormData(formElement);
  const payload = {
    name: form.get("name"),
    subject: form.get("subject"),
    goal: form.get("goal"),
    description: form.get("description"),
    schedule: form.get("schedule"),
    meetingMode: form.get("meetingMode"),
    meetingLocation: form.get("meetingLocation"),
    level: form.get("level"),
    maxMembers: Number(form.get("maxMembers"))
  };

  try {
    const group = await requestJson(`${window.location.origin}/api/groups`, {
      method: "POST",
      body: JSON.stringify(payload)
    });
    showNotice(`Đã tạo nhóm ${group.name}. Đang chờ read model cập nhật...`);
    await new Promise((resolve) => setTimeout(resolve, 1200));
    currentPage = 1;
    currentSearch = "";
    document.querySelector("#search-form").reset();
    await loadGroups();
    formElement.reset();
    selectWorkspaceView("discover");
  } catch (error) {
    showNotice(error.message, true);
  }
});

document.querySelector("#search-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  currentSearch = new FormData(event.currentTarget).get("search").trim();
  currentPage = 1;
  try {
    await loadGroups();
  } catch (error) {
    showNotice(error.message, true);
  }
});

document.querySelector("#refresh-button").addEventListener("click", async () => {
  try {
    await loadGroups();
    showNotice("Danh sách nhóm đã được làm mới.");
  } catch (error) {
    showNotice(error.message, true);
  }
});

document.querySelector("#previous-page").addEventListener("click", async () => {
  if (currentPage > 1) {
    currentPage -= 1;
    await loadGroups();
  }
});

document.querySelector("#next-page").addEventListener("click", async () => {
  if (currentPage * pageSize < totalCount) {
    currentPage += 1;
    await loadGroups();
  }
});

async function handleGroupListClick(event) {
  const button = event.target.closest("button[data-action]");
  if (!button) {
    return;
  }

  const card = button.closest(".group-card");
  const groupId = card.dataset.groupId;
  try {
    if (button.dataset.action === "delete-group") {
      const confirmed = window.confirm(
        "Xóa vĩnh viễn nhóm này? Thành viên và các yêu cầu tham gia liên quan cũng sẽ bị xóa. Thao tác này không thể hoàn tác."
      );
      if (!confirmed) {
        return;
      }

      await requestJson(`${window.location.origin}/api/groups/${groupId}`, {
        method: "DELETE"
      });
      showNotice("Đã xóa nhóm. Đang đồng bộ danh sách...");
      await new Promise((resolve) => setTimeout(resolve, 1200));
      await loadGroups();
      if (!document.querySelector("#mine-view").hidden) {
        await loadMyGroups();
      }
    } else if (button.dataset.action === "leave-group") {
      const confirmed = window.confirm("Bạn có chắc muốn rời nhóm này không?");
      if (!confirmed) {
        return;
      }

      await requestJson(
        `${window.location.origin}/api/groups/${groupId}/members/me`,
        { method: "DELETE" }
      );
      showNotice("Bạn đã rời nhóm. Đang đồng bộ danh sách...");
      await new Promise((resolve) => setTimeout(resolve, 1200));
      await loadGroups();
      if (!document.querySelector("#mine-view").hidden) {
        await loadMyGroups();
      }
    } else if (button.dataset.action === "show-join") {
      card.querySelector(".inline-form")?.remove();
      const form = document.createElement("form");
      form.className = "inline-form";
      const messageLabel = document.createElement("label");
      messageLabel.textContent = "Lời nhắn";
      const messageInput = document.createElement("textarea");
      messageInput.name = "message";
      messageInput.maxLength = 1000;
      messageInput.rows = 2;
      messageInput.placeholder = "Giới thiệu ngắn về mong muốn tham gia";
      messageLabel.append(messageInput);
      form.append(messageLabel);
      const submit = document.createElement("button");
      submit.className = "button button-primary";
      submit.type = "submit";
      submit.textContent = "Gửi yêu cầu";
      form.append(submit);
      card.append(form);
      form.addEventListener("submit", async (submitEvent) => {
        submitEvent.preventDefault();
        const values = new FormData(form);
        try {
          const created = await requestJson(
            `${window.location.origin}/api/groups/${groupId}/join-requests`,
            {
              method: "POST",
              body: JSON.stringify({ message: values.get("message") })
            }
          );
          showNotice(`Đã gửi yêu cầu tham gia (${created.status}).`);
          form.remove();
        } catch (error) {
          showNotice(error.message, true);
        }
      });
    } else if (button.dataset.action === "show-requests") {
      await loadRequests(card);
    } else if (button.dataset.action === "approve" || button.dataset.action === "reject") {
      const action = button.dataset.action;
      const updated = await requestJson(
        `${window.location.origin}/api/groups/${groupId}/join-requests/${button.dataset.requestId}/${action}`,
        { method: "POST" }
      );
      showNotice(`Yêu cầu đã được ${updated.status === "Approved" ? "duyệt" : "từ chối"}.`);
      await loadGroups();
      if (!document.querySelector("#mine-view").hidden) {
        await loadMyGroups();
      }
    }
  } catch (error) {
    showNotice(error.message, true);
  }
}

[groupsElement, ownedGroupsElement, joinedGroupsElement].forEach((list) => {
  list.addEventListener("click", handleGroupListClick);
});

selectAuthForm("login");
selectWorkspaceView("discover");
updateAuthUi();
