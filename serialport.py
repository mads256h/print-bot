import sys
import serial
import threading

serialPort = sys.argv[1]
baudrate = sys.argv[2]

port = serial.Serial(serialPort, baudrate)


def redir_serial_to_console():
    while(True):
        print(port.readline().decode(), end="")
        sys.stdout.flush()


readThread = threading.Thread(target=redir_serial_to_console)
readThread.start()

for line in sys.stdin:
    port.write(line.encode() + b"\n")

