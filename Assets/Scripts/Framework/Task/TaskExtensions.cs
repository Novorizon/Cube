///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2022-10-11
/// Description：Task扩展
///------------------------------------
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Framework
{
    public static class TaskExtensions
    {
        public static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public static async void Forget<T>(this Task<T> task)
        {
            try
            {
                _ = await task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }


}