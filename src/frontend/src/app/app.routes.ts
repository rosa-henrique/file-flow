import { Routes } from '@angular/router';
import { Home } from './home/home';
import { CreateUploadBatch } from './create-upload-batch/create-upload-batch';

export const routes: Routes = [
  {
    path: '',
    component: Home,
  },
  {
    path: 'create-upload-batch',
    component: CreateUploadBatch,
  },
];
