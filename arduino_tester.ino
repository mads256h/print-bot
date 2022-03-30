char buf[256];

char readchar() {
  int c = -1;
  while(c == -1)
  {
    c = Serial.read();
  }

  return (char)c;
}

String readline() {
  int bufpos = -1;
  do {
    bufpos++;
    buf[bufpos] = readchar();
  } while(buf[bufpos] != '\n');

  buf[bufpos + 1] = 0;

  return String(buf);
}

void setup() {
  // put your setup code here, to run once:
  Serial.begin(9600);
  Serial.write("start\n");
  Serial.write("echo:Marlin 1.0.0\n");
  Serial.write("echo: Last Updated: Mar 15 2018 13:04:08 | Author: Vers:_3.3.0\n");
  Serial.write("Compiled: Mar 15 2018\n");
  Serial.write("echo: Free Memory: 2055  PlannerBufferBytes: 1232\n");
  Serial.write("echo:Stored settings retrieved\n");
  Serial.write("echo:Steps per unit:\n");
  Serial.write("echo:  M92 X80.00 Y80.00 Z200.00 E369.00\n");
  Serial.write("echo:Maximum feedrates (mm/s):\n");
  Serial.write("echo:  M203 X300.00 Y300.00 Z40.00 E45.00\n");
  Serial.write("echo:Maximum Acceleration (mm/s2):\n");
  Serial.write("echo:  M201 X9000 Y9000 Z100 E10000\n");
  Serial.write("echo:Acceleration: S=acceleration, T=retract acceleration\n");
  Serial.write("echo:  M204 S5000.00 T3000.00\n");
  Serial.write("echo:Advanced variables: S=Min feedrate (mm/s), T=Min travel feedrate (mm/s), B=minimum segment time (ms), X=maximum XY jerk (mm/s),  Z=maximum Z jerk (mm/s),  E=maximum E jerk (mm/s)\n");
  Serial.write("echo:  M205 S0.00 T0.00 B20000 X30.00 Z0.40 E5.00\n");
  Serial.write("echo:Home offset (mm):\n");
  Serial.write("echo:  M206 X0.00 Y0.00 Z-14.65\n");
  Serial.write("echo:PID settings:\n");
  Serial.write("echo:   M301 P10.03 I1.50 D70.00\n");
  Serial.write("echo:SD card ok\n");      
}

void loop() {
  auto line = readline();
  if (line.startsWith("M105")) {
    Serial.write("ok T:251 B:200\n");
    return;
  }
  if (line.startsWith("M190")) {
    for (int i = 0; i < 10; i++) {
      Serial.write("T:201.2 E:0 B:58.2\n");
      delay(2000);
    }
  }
  // put your main code here, to run repeatedly:
  Serial.write("ok\n");
  delay(2000);
}
