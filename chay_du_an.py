"""Start the StudySpace services and their local dependencies on Windows."""

import os
import re
import secrets
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import webbrowser
from pathlib import Path


ROOT = Path(__file__).resolve().parent
COMMAND_PROJECT = ROOT / "CommandService"
QUERY_PROJECT = ROOT / "QueryService"
CONSUMER_PROJECT = ROOT / "ConsumerService"
COMMAND_URL = "http://localhost:5088/"
QUERY_SWAGGER_URL = "http://localhost:5135/swagger/index.html"
KAFKA_PORT = 9092
MONGODB_PORT = 27017
START_TIMEOUT_SECONDS = 120


def configure_console():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")


def handle_console_break(_signal_number, _frame):
    raise KeyboardInterrupt


def capture(command, cwd=ROOT):
    try:
        return subprocess.run(
            command,
            cwd=str(cwd),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
    except FileNotFoundError as error:
        raise RuntimeError("Không tìm thấy lệnh: " + command[0]) from error


def run_checked(command, cwd=ROOT):
    result = subprocess.run(command, cwd=str(cwd), check=False)
    if result.returncode != 0:
        raise RuntimeError(
            "Lệnh thất bại (mã "
            + str(result.returncode)
            + "): "
            + subprocess.list2cmdline(command)
        )


def check_dotnet():
    sdk_result = capture(["dotnet", "--list-sdks"])
    if sdk_result.returncode != 0:
        raise RuntimeError("Không chạy được .NET SDK. Hãy cài .NET 9 SDK trở lên.")

    sdk_versions = re.findall(r"^(\d+)\.", sdk_result.stdout, re.MULTILINE)
    if not any(int(major) >= 9 for major in sdk_versions):
        raise RuntimeError("Dự án cần .NET 9 SDK trở lên.")

    runtime_result = capture(["dotnet", "--list-runtimes"])
    runtimes = runtime_result.stdout
    has_net9_runtime = re.search(
        r"^Microsoft\.NETCore\.App 9\.", runtimes, re.MULTILINE
    )
    has_aspnet_runtime = re.search(
        r"^Microsoft\.AspNetCore\.App 9\.", runtimes, re.MULTILINE
    )
    if not has_net9_runtime or not has_aspnet_runtime:
        raise RuntimeError(
            "Thiếu .NET 9 Runtime hoặc ASP.NET Core 9 Runtime. "
            "Hãy cài .NET 9 SDK."
        )


def port_is_open(port):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=1):
            return True
    except OSError:
        return False


def wait_for_port(port, label, timeout=START_TIMEOUT_SECONDS):
    print("Đang chờ " + label + "...")
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if port_is_open(port):
            print(label + " đã sẵn sàng.")
            return
        time.sleep(1)
    raise RuntimeError(
        "Quá thời gian chờ " + label + " tại localhost:" + str(port) + "."
    )


def service_state(service_name):
    result = capture(["sc.exe", "query", service_name])
    match = re.search(r"STATE\s*:\s*(\d+)", result.stdout)
    if result.returncode != 0 or not match:
        return None
    return int(match.group(1))


def ensure_windows_service(service_name, display_name, fallback_port=None):
    state = service_state(service_name)
    if state is None:
        if fallback_port and port_is_open(fallback_port):
            print(display_name + " đang hoạt động.")
            return
        raise RuntimeError(
            "Không tìm thấy dịch vụ Windows "
            + service_name
            + ". Hãy kiểm tra cài đặt "
            + display_name
            + "."
        )

    if state != 4:
        print("Đang khởi động " + display_name + "...")
        result = capture(["sc.exe", "start", service_name])
        if result.returncode != 0 and service_state(service_name) != 4:
            raise RuntimeError(
                "Không thể khởi động "
                + display_name
                + ". Hãy mở VS Code bằng quyền quản trị hoặc khởi động dịch vụ "
                "trong Windows Services."
            )

        deadline = time.monotonic() + START_TIMEOUT_SECONDS
        while time.monotonic() < deadline:
            if service_state(service_name) == 4:
                break
            time.sleep(1)
        else:
            raise RuntimeError("Khởi động " + display_name + " quá thời gian.")

    if fallback_port:
        wait_for_port(fallback_port, display_name)
    else:
        print(display_name + " đang hoạt động.")


def docker_info():
    return capture(["docker", "info", "--format", "{{.ServerVersion}}"])


def docker_desktop_path():
    candidates = [
        Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
        / "Docker"
        / "Docker"
        / "Docker Desktop.exe",
        Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"))
        / "Docker"
        / "Docker"
        / "Docker Desktop.exe",
        Path(os.environ.get("LOCALAPPDATA", ""))
        / "Programs"
        / "Docker"
        / "Docker"
        / "Docker Desktop.exe",
    ]
    return next((candidate for candidate in candidates if candidate.is_file()), None)


def ensure_docker_engine():
    result = docker_info()
    if result.returncode == 0:
        return

    desktop = docker_desktop_path()
    if desktop is None:
        raise RuntimeError(
            "Docker Desktop chưa chạy hoặc chưa được cài. "
            "Hãy mở Docker Desktop rồi chạy lại file này."
        )

    print("Đang mở Docker Desktop...")
    try:
        subprocess.Popen(
            [str(desktop)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=getattr(subprocess, "DETACHED_PROCESS", 0),
        )
    except OSError as error:
        raise RuntimeError("Không mở được Docker Desktop: " + str(error)) from error

    deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        if docker_info().returncode == 0:
            print("Docker Desktop đã sẵn sàng.")
            return
        time.sleep(3)
    raise RuntimeError(
        "Docker Desktop chưa sẵn sàng sau 3 phút. "
        "Hãy kiểm tra Docker Desktop rồi chạy lại."
    )


def read_signing_key(project):
    result = capture(
        ["dotnet", "user-secrets", "list", "--project", str(project)]
    )
    if result.returncode != 0:
        raise RuntimeError(
            "Không đọc được .NET user-secrets cho "
            + project.name
            + ". Kiểm tra cấu hình UserSecretsId trong project."
        )

    for line in result.stdout.splitlines():
        name, separator, value = line.partition("=")
        if separator and name.strip() == "Authentication:SigningKey":
            key = value.strip()
            if len(key.encode("utf-8")) >= 32:
                return key
    return None


def set_signing_key(project, key):
    result = capture(
        [
            "dotnet",
            "user-secrets",
            "set",
            "Authentication:SigningKey",
            key,
            "--project",
            str(project),
        ]
    )
    if result.returncode != 0:
        raise RuntimeError(
            "Không lưu được JWT signing key vào user-secrets cho "
            + project.name
            + "."
        )


def ensure_shared_signing_key():
    command_key = read_signing_key(COMMAND_PROJECT)
    query_key = read_signing_key(QUERY_PROJECT)

    if command_key and query_key and command_key != query_key:
        print("Đang đồng bộ JWT signing key giữa hai API...")
        key = command_key
    else:
        key = command_key or query_key or secrets.token_urlsafe(48)

    if command_key != key:
        set_signing_key(COMMAND_PROJECT, key)
    if query_key != key:
        set_signing_key(QUERY_PROJECT, key)

    print("JWT signing key đã được kiểm tra.")


def http_is_ready(url):
    try:
        with urllib.request.urlopen(url, timeout=2) as response:
            return 200 <= response.status < 400
    except (urllib.error.URLError, TimeoutError, OSError):
        return False


def start_service(label, url, port, command, processes):
    if http_is_ready(url):
        print(label + " đã chạy sẵn; dùng lại service hiện có.")
        return None

    if port_is_open(port):
        raise RuntimeError(
            "Cổng "
            + str(port)
            + " đang được ứng dụng khác sử dụng. Hãy giải phóng cổng rồi chạy lại."
        )

    print("Đang khởi động " + label + "...")
    creation_flags = getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)
    process = subprocess.Popen(
        command,
        cwd=str(ROOT),
        creationflags=creation_flags,
    )
    processes.append((label, process))
    return process


def wait_for_http(label, url, process, timeout=START_TIMEOUT_SECONDS):
    if process is None:
        return

    print("Đang chờ " + label + " khởi động...")
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(
                label
                + " đã dừng trong lúc khởi động. Xem thông báo lỗi phía trên."
            )
        if http_is_ready(url):
            print(label + " đã sẵn sàng.")
            return
        time.sleep(1)
    raise RuntimeError(label + " khởi động quá thời gian.")


def stop_processes(processes):
    for label, process in reversed(processes):
        if process.poll() is not None:
            continue
        print("Đang dừng " + label + "...")
        try:
            if os.name == "nt":
                process.send_signal(signal.CTRL_BREAK_EVENT)
            else:
                process.send_signal(signal.SIGINT)
            process.wait(timeout=10)
        except (OSError, subprocess.TimeoutExpired):
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


def main():
    configure_console()
    if os.name != "nt":
        print("File này được thiết kế để chạy trên Windows.")
        return 1
    if hasattr(signal, "SIGBREAK"):
        signal.signal(signal.SIGBREAK, handle_console_break)

    processes = []
    exit_code = 0
    try:
        check_dotnet()
        ensure_windows_service("MSSQL$SQLEXPRESS", "SQL Server Express")
        ensure_windows_service("MongoDB", "MongoDB", MONGODB_PORT)

        ensure_docker_engine()
        print("Đang khởi động Kafka và ZooKeeper bằng Docker Compose...")
        run_checked(["docker", "compose", "up", "-d"])
        wait_for_port(KAFKA_PORT, "Kafka")

        ensure_shared_signing_key()

        command = start_service(
            "CommandService",
            COMMAND_URL,
            5088,
            [
                "dotnet",
                "run",
                "--project",
                str(COMMAND_PROJECT),
                "--launch-profile",
                "http",
            ],
            processes,
        )
        query = start_service(
            "QueryService",
            QUERY_SWAGGER_URL,
            5135,
            [
                "dotnet",
                "run",
                "--project",
                str(QUERY_PROJECT),
                "--launch-profile",
                "http",
            ],
            processes,
        )
        consumer = subprocess.Popen(
            ["dotnet", "run", "--project", str(CONSUMER_PROJECT)],
            cwd=str(ROOT),
            creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
        )
        processes.append(("ConsumerService", consumer))

        wait_for_http("CommandService", COMMAND_URL, command)
        wait_for_http("QueryService", QUERY_SWAGGER_URL, query)
        time.sleep(2)
        if consumer.poll() is not None:
            raise RuntimeError(
                "ConsumerService đã dừng khi khởi động. Xem thông báo lỗi phía trên."
            )

        print()
        print("Tất cả service đã chạy.")
        print("Giao diện:  " + COMMAND_URL)
        print("Swagger:    http://localhost:5088/swagger")
        print("Swagger:    http://localhost:5135/swagger")
        print("Nhấn Ctrl+C để dừng ba service .NET.")
        print("Kafka, SQL Server và MongoDB sẽ tiếp tục chạy.")
        webbrowser.open(COMMAND_URL)

        while True:
            for label, process in processes:
                if process.poll() is not None:
                    raise RuntimeError(label + " đã dừng bất ngờ.")
            time.sleep(1)
    except KeyboardInterrupt:
        print("\nĐang dừng các service .NET...")
    except RuntimeError as error:
        print("\nLỖI: " + str(error), file=sys.stderr)
        exit_code = 1
    finally:
        stop_processes(processes)

    if exit_code and sys.stdin.isatty():
        try:
            input("Nhấn Enter để đóng cửa sổ...")
        except EOFError:
            pass
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
