import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateUploadBatch } from './create-upload-batch';

describe('CreateUploadBatch', () => {
  let component: CreateUploadBatch;
  let fixture: ComponentFixture<CreateUploadBatch>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateUploadBatch],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateUploadBatch);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
