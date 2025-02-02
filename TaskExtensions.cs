/*
 * TaskExtensions.cs
 *
 * Extension methods for executing asynchronous tasks synchronously.
 * Provides methods to execute async methods synchronously and handle the result.
 *
 * Copyright (c) 2024 Bashkardin Pavel
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/// <summary>
/// Provides extension methods for executing asynchronous tasks synchronously.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Executes an asynchronous method synchronously and returns its result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the asynchronous method.</typeparam>
    /// <param name="task">The asynchronous task to execute.</param>
    /// <returns>The result of the asynchronous operation.</returns>
    public static T RunSync<T>(this Task<T> task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes an asynchronous method synchronously without returning a result.
    /// </summary>
    /// <param name="task">The asynchronous task to execute.</param>
    public static void RunSync(this Task task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        task.GetAwaiter().GetResult();
    }
}