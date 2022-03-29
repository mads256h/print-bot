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
}

void loop() {
  auto line = readline();
  if (line.startsWith("M105")) {
    Serial.write("ok T:251 B:200\n");
    return;
  }
  if (line.startsWith("M190")) {
    for (int i = 0; i < 10; i++) {
      Serial.write("T:201 B:117\n");
      delay(2000);
    }
  }
  // put your main code here, to run repeatedly:
  Serial.write("ok\n");
  delay(2000);
}