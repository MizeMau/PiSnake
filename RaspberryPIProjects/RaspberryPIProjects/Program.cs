using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.I2c;
using System.Threading.Tasks;

class Program
{
    private static bool joystickPressed = false;
    private static I2cDevice i2cDevice;

    private static int width = 40;
    private static int height = 20;
    private static List<(int x, int y)> snake = new List<(int x, int y)> { (20, 10) };
    private static (int x, int y) food;
    private static (int x, int y) direction = (1, 0);
    private static bool gameRunning = true;
    private static int score = 0;

    static async Task<int> Main()
    {
        var controller = new GpioController();

        var i2cSettings = new I2cConnectionSettings(1, 0x4B);
        i2cDevice = I2cDevice.Create(i2cSettings);
        var inputTask = new Task(Input);
        inputTask.Start();

        Console.CursorVisible = false;

        do
        {
            Console.Clear();
            Console.SetWindowSize(width + 2, height + 2);
            DrawBorders();
            SpawnFood();
            while (gameRunning)
            {
                Update();
                await Task.Delay(125);
            }
            joystickPressed = false;
            Console.SetCursorPosition(0, height + 2);
            Console.WriteLine("Game Over! Your score: " + score);

            Console.WriteLine();
            Console.WriteLine("Um erneut zu spielen, bitte den Joystick drücken");
            controller.OpenPin(18, PinMode.InputPullUp);
            controller.RegisterCallbackForPinValueChangedEvent(18, PinEventTypes.Falling, OnPinEvent);
            while (!joystickPressed) {
                await Task.Delay(10);
            }
            snake = new List<(int x, int y)> { (20, 10) };
            direction = (1, 0);
            score = 0;
            gameRunning = true;
            controller.UnregisterCallbackForPinValueChangedEvent(18, OnPinEvent);
            controller.ClosePin(18);
        } while (true);
    }

    static void DrawBorders()
    {
        for (int i = 0; i <= width; i++)
        {
            Console.SetCursorPosition(i, 0);
            Console.Write("#");
            Console.SetCursorPosition(i, height);
            Console.Write("#");
        }
        for (int i = 0; i <= height; i++)
        {
            Console.SetCursorPosition(0, i);
            Console.Write("#");
            Console.SetCursorPosition(width, i);
            Console.Write("#");
        }
    }

    static void SpawnFood()
    {
        Random rnd = new Random();
        food = (rnd.Next(1, width - 1), rnd.Next(1, height - 1));
        Console.SetCursorPosition(food.x, food.y);
        Console.Write("O");
    }

    static async void Input()
    {
        while (true)
        {
            byte adcValueX = ReadChannel(i2cDevice, 4);
            byte adcValueY = ReadChannel(i2cDevice, 0);

            int x = adcValueX <= 60 ? -1 : 0;
            if (adcValueX >= 195) x = 1;
            int y = adcValueY <= 60 ? -1 : 0;
            if (adcValueY >= 195) y = 1;

            if (y != 0 && x != 0)
            {
                if (adcValueX < adcValueY)
                {
                    x = 0;
                }
                else
                {
                    y = 0;
                }
            }

            if (x != 0 || y != 0)
                direction = (x, y);

            await Task.Delay(10);
        }
    }

    static void Update()
    {
        var newHead = (snake[0].x + direction.x, snake[0].y + direction.y);

        // Check if snake hits the wall or itself
        if (newHead.Item1 <= 0 || newHead.Item1 >= width || newHead.Item2 <= 0 || newHead.Item2 >= height || snake.Contains(newHead))
        {
            gameRunning = false;
            return;
        }

        // Draw new head
        Console.SetCursorPosition(newHead.Item1, newHead.Item2);
        Console.Write("■");

        // Move snake
        snake.Insert(0, newHead);

        // Check if snake eats food
        if (newHead == food)
        {
            score += 10;
            SpawnFood();
        }
        else
        {
            // Remove tail
            var tail = snake[^1];
            Console.SetCursorPosition(tail.x, tail.y);
            Console.Write(" ");
            snake.RemoveAt(snake.Count - 1);
        }

        // Display score
        Console.SetCursorPosition(0, height + 1);
        Console.Write("Score: " + score);
    }

    static void OnPinEvent(object sender, PinValueChangedEventArgs args)
    {
        joystickPressed = args.ChangeType == PinEventTypes.Falling;
        Console.SetCursorPosition(0, height + 6);
        Console.WriteLine("Pressed: " + (args.ChangeType == PinEventTypes.Falling));
    }

    static byte ReadChannel(I2cDevice device, int channel)
    {
        byte controlByte = (byte)(0x84 | (channel << 4));
        device.WriteByte(controlByte);

        byte[] buffer = new byte[1];
        device.Read(buffer);

        return buffer[0];
    }
}
